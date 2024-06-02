module FryProxy.IO.BufferedParser.Parser

open System
open System.Collections.Generic
open System.Threading
open System.Threading.Tasks
open FryProxy.IO
open Microsoft.FSharp.Core


module ParseResult =

    let inline lift (task: 'a Task) = ValueTask<'a>(task)

    let err = ValueTask.FromException<ParseState * 'a>

    let inline cancel () =
        ValueTask.FromCanceled<ParseState * 'a>(CancellationToken(true))

    let (|Faulted|Pending|Cancelled|Successful|) (task: ValueTask<_>) =
        if task.IsFaulted then Faulted task
        elif task.IsCanceled then Cancelled task
        elif task.IsCompletedSuccessfully then Successful task
        else Pending task

    let rec bind (binder: 'a -> 'b ValueTask) (valueTask: 'a ValueTask) =
        if valueTask.IsCompletedSuccessfully then
            binder valueTask.Result
        else
            task {
                let! res = valueTask
                return! binder res
            }
            |> lift

    let map fn = bind (fn >> ValueTask.FromResult)

/// Parser evaluating to a constant value.
let inline unit a : Parser<'a> =
    fun (_, s) -> ValueTask.FromResult(s, a)

/// Parser evaluating to a raw buffer content.
let inline bytes (n: uint64) : Parser<IReadOnlyBytes> =
    //TODO: restrict reads unless given number of bytes had been read.
    fun (rb, s) -> ValueTask.FromResult(s, rb)

/// Failed parser.
let inline failed reason : Parser<'a> =
    fun _ -> ParseResult.err (ParseError reason)

/// Execute parsers sequentially
let inline bind (binder: 'a -> Parser<'b>) (parser: Parser<'a>) : Parser<'b> =
    fun (rb, s) -> parser (rb, s) |> ParseResult.bind (fun (s', a) -> binder a (rb, s'))

/// Transform value inside parser.
let inline map fn (parser: Parser<'a>) : Parser<'b> =
    parser >> ParseResult.map (fun (s', a) -> (s', fn a))

/// Unwrap parsed value option, failing parser when empty.
let inline flatmap (fn: 'a -> 'b Option) (parser: Parser<'a>) : Parser<'b> =
    parser
    >> ParseResult.bind (fun (s, a) ->
        match fn a with
        | Some b -> ValueTask.FromResult(s, b)
        | None -> ParseResult.err (ParseError $"failed {typeof<'a>} -> {typeof<'b>}"))

/// Discard bytes consumed by parser when it succeeds.
let inline commit (parser: Parser<'a>) : Parser<'a> =
    fun (rb, s) ->
        parser (rb, s)
        |> ParseResult.map (fun (s', a) ->
            match s' with
            | ParseState(0us) -> s', a
            | ParseState(lo) ->
                rb.Discard(int lo)
                (ParseState.Zero, a))

/// Commit and execute parser, returning parsed value.
let inline run rb (parser: Parser<'a>) : 'a ValueTask =
    (rb, ParseState.Zero) |> commit parser |> ParseResult.map snd

/// Apply parser until condition evaluates to true or parser fails.
/// Returns the results of the last applied parser.
let takeWhile (cond: unit -> bool) (parser: Parser<unit>) : Parser<unit> =
    let rec loop (rb, s) =
        let mutable tsk = ValueTask.FromResult(s, ())

        while tsk.IsCompletedSuccessfully && cond () do
            let s', _ = tsk.Result
            tsk <- parser (rb, s')

        if tsk.IsCanceled || tsk.IsFaulted || not (cond ()) then
            tsk
        else
            task {
                let! s', _ = tsk
                return! loop (rb, s')
            }
            |> ParseResult.lift

    loop

/// Commit sub-parser repeatedly as long as it succeeds and return results as list.
let eager (parser: Parser<'a>) : Parser<'a list> =
    let rec loop (acc: 'a list) (rb, s) =
        let mutable s' = s
        let mutable xs = acc
        let mutable tsk = parser (rb, s)

        while tsk.IsCompletedSuccessfully do
            let s'', x = tsk.Result
            s' <- s''
            xs <- x :: xs
            tsk <- parser (rb, s'')

        if tsk.IsFaulted then
            ValueTask.FromResult(s', List.rev xs)
        else if tsk.IsCanceled then
            ParseResult.cancel ()
        else
            task {
                let! s'', x = tsk
                return! loop (x :: xs) (rb, s'')
            }
            |> ParseResult.lift

    loop List.empty

let unfold
    (generator: 'T -> Parser<'T * 'V>)
    (state: 'T) //TODO: merge generator and parser state?
    (rb: ReadBuffer, ps: ParseState)
    : 'V IAsyncEnumerable ParseResult =
    let mutable genState = state
    let mutable parseState = ps
    let mutable value: 'V = Unchecked.defaultof<'V>

    let transition (parseState', (genState', value')) =
        parseState <- parseState'
        genState <- genState'
        value <- value'
        true

    let next (ct: CancellationToken) =
        if ct.IsCancellationRequested then
            ValueTask.FromCanceled<bool>(ct)
        else
            match generator genState (rb, parseState) |> ParseResult.map transition with
            | ParseResult.Faulted t ->
                try
                    ValueTask.FromResult(t.Result)
                with ParseError _ ->
                    ValueTask.FromResult(false)
            | task -> task

    let enumerator ct =
        { new IAsyncEnumerator<'V> with
            override this.MoveNextAsync() = next ct
            override this.Current = value

          interface IAsyncDisposable with
              override this.DisposeAsync() = ValueTask.CompletedTask }

    let enumerable =
        { new IAsyncEnumerable<'V> with
            override this.GetAsyncEnumerator ct = enumerator ct }

    ValueTask.FromResult(parseState, enumerable)


/// Ignore parsed value.
let inline ignore p = map ignore p

/// Fail the parser unless parsed value satisfies given condition.
let inline must msg cond =
    bind (fun a -> if cond a then unit a else failed $"{a} is not {msg}")

/// <summary>
/// Create a parser consuming buffered bytes on each successful read.
/// </summary>
/// <param name="parseBytes">
/// Byte parsing function which is given reader buffer and returns
/// an optional tuple of consumed bytes count and parsed value.
/// </param>
let parseBuffer parseBytes : Parser<'a> =
    fun (rb, ParseState(offset)) ->
        let parseMem (mem: ReadOnlyMemory<byte>) =
            if mem.Length <= int offset then
                None
            else
                mem.Slice(int offset).ToArray()
                |> parseBytes
                |> Option.map (fun (n, v) -> ParseState(offset + n), v)

        match parseMem rb.Pending with
        | Some(s, x) -> ValueTask.FromResult(s, x)
        | None when rb.PendingSize < rb.Capacity ->
            task {
                let! pending = rb.Pick()

                return!
                    match parseMem pending with
                    | Some(s, x) -> ValueTask.FromResult(s, x)
                    | None -> ParseResult.err (ParseError $"{typeof<'a>}")
            }
            |> ParseResult.lift
        | None -> ParseResult.err (ParseError "Offset beyond buffer size")
