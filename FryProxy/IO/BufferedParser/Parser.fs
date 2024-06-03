module FryProxy.IO.BufferedParser.Parser

open System
open System.Collections.Generic
open System.Threading
open System.Threading.Tasks
open FryProxy.IO
open Microsoft.FSharp.Core


module ParseResult =

    let inline liftTask (task: 'a Task) = ValueTask<'a>(task)

    let err = ValueTask.FromException<ParseState * 'a>

    let inline cancel () =
        ValueTask.FromCanceled<ParseState * 'a>(CancellationToken(true))

    let (|Faulted|Pending|Cancelled|Successful|) (task: ValueTask<_>) =
        if task.IsFaulted then
            Faulted task
        elif task.IsCanceled then
            Cancelled task
        elif task.IsCompletedSuccessfully then
            Successful task
        else
            Pending task

    let rec bind (binder: 'a -> 'b ValueTask) (valueTask: 'a ValueTask) =
        if valueTask.IsCompletedSuccessfully then
            binder valueTask.Result
        else
            task {
                let! res = valueTask
                return! binder res
            }
            |> liftTask

    let map fn = bind(fn >> ValueTask.FromResult)

/// Force lazy parser evaluation, failing if not yet completed.
let strict (parser: 'a Parser) : 'a Parser =
    fun (rb, s) ->
        match s with
        | { Mode = StrictMode } -> parser(rb, s)
        | { Mode = LazyMode x } when x.Consumed -> parser(rb, { s with Mode = StrictMode })
        | _ -> ParseResult.err(ParseError "Parser has not finished yet")

/// Parser evaluating to a constant value.
let inline unit a : Parser<'a> =
    fun (_, s) -> ValueTask.FromResult(s, a)

/// Parser evaluating to a raw buffer content.
let inline bytes (n: uint64) : Parser<IByteBuffer> =
    strict
    <| fun (rb, _) ->
        let span = BufferSpan(rb, n)
        let state = { Offset = 0us; Mode = LazyMode(span) }
        ValueTask.FromResult(state, span)

/// Failed parser.
let inline failed reason : Parser<'a> =
    fun _ -> ParseResult.err(ParseError reason)

/// Execute parsers sequentially.
let bind (binder: 'a -> Parser<'b>) (parser: Parser<'a>) : Parser<'b> =
    fun (rb, s) ->
        ParseResult.liftTask
        <| task {
            let! s', a = parser(rb, s)
            return! binder a (rb, s')
        }



/// Transform value inside parser.
let inline map fn (parser: Parser<'a>) : Parser<'b> =
    parser >> ParseResult.map(fun (s', a) -> (s', fn a))

/// Ignore parsed value.
let inline ignore p = map ignore p

/// Unwrap parsed value option, failing parser when empty.
let inline flatmap (fn: 'a -> 'b Option) (parser: Parser<'a>) : Parser<'b> =
    parser
    >> ParseResult.bind(fun (s, a) ->
        match fn a with
        | Some b -> ValueTask.FromResult(s, b)
        | None -> ParseResult.err(ParseError $"failed {typeof<'a>} -> {typeof<'b>}"))

/// Fail the parser unless parsed value satisfies given condition.
let inline must msg cond parser : Parser<_> =
    let validate (s, a) =
        if cond a then
            ValueTask.FromResult(s, a)
        else
            ParseResult.err(ParseError $"{a} is not {msg}")

    parser >> ParseResult.bind validate

/// Discard bytes consumed by parser when it succeeds.
let inline commit (parser: Parser<'a>) : Parser<'a> =
    fun (rb, s) ->
        parser(rb, s)
        |> ParseResult.map(fun (s', a) ->
            match s' with
            | { Offset = 0us } -> s', a
            | { Offset = lo } ->
                rb.Discard(int lo)
                { s' with Offset = 0us }, a)

/// Commit and execute parser, returning parsed value.
let inline run rb (parser: Parser<'a>) : 'a ValueTask =
    (rb, ParseState.Zero) |> commit parser |> ParseResult.map snd

/// Apply parser until condition evaluates to true or parser fails.
/// Returns the results of the last applied parser.
let takeWhile (cond: unit -> bool) (parser: Parser<unit>) : Parser<unit> =
    let rec loop (rb, s) =
        let mutable tsk = ValueTask.FromResult(s, ())

        while tsk.IsCompletedSuccessfully && cond() do
            let s', _ = tsk.Result
            tsk <- parser(rb, s')

        if tsk.IsCanceled || tsk.IsFaulted || not(cond()) then
            tsk
        else
            ParseResult.liftTask
            <| task {
                let! s', _ = tsk
                return! loop(rb, s')
            }

    loop

/// Commit sub-parser repeatedly as long as it succeeds and return results as list.
let eager (parser: Parser<'a>) : Parser<'a list> =
    let rec loop (acc: 'a list) (rb, s) =
        let mutable s' = s
        let mutable xs = acc
        let mutable tsk = parser(rb, s)

        while tsk.IsCompletedSuccessfully do
            let s'', x = tsk.Result
            s' <- s''
            xs <- x :: xs
            tsk <- parser(rb, s'')

        if tsk.IsFaulted then
            ValueTask.FromResult(s', List.rev xs)
        else if tsk.IsCanceled then
            ParseResult.cancel()
        else
            task {
                let! s'', x = tsk
                return! loop (x :: xs) (rb, s'')
            }
            |> ParseResult.liftTask

    loop List.empty

let unfold (gen: 'a LazySeqGen) : 'a IAsyncEnumerable Parser =
    strict
    <| fun (rb, s) ->
        let it = LazyIter(gen, rb, s)

        let enumerable =
            { new IAsyncEnumerable<_> with
                override this.GetAsyncEnumerator ct = it.WithToken(ct) }

        ValueTask.FromResult({ s with Offset = 0us; Mode = LazyMode(it) }, enumerable)

/// <summary>
/// Create a parser consuming buffered bytes on each successful read.
/// </summary>
/// <param name="decode">
/// Byte parsing function which is given reader buffer and returns
/// an optional tuple of consumed bytes count and parsed value.
/// </param>
let decoder decode : Parser<'a> =
    strict
    <| fun (rb, s) ->
        let offset = int s.Offset

        let fail cause =
            ParseResult.err(ParseError $"Decoding {typeof<'a>} failed: {cause}")

        let tryDecode (mem: ReadOnlyMemory<byte>) =
            mem.Slice(offset).ToArray()
            |> decode
            |> Option.map(fun (n, v) -> { s with Offset = s.Offset + n }, v)

        if offset > rb.Capacity then
            fail "offset beyond capacity"
        else
            match tryDecode rb.Pending with
            | Some(s, x) -> ValueTask.FromResult(s, x)
            | None ->
                task {
                    let! pending = rb.Pick()

                    match tryDecode pending with
                    | Some(s, x) -> return (s, x)
                    | None -> return! fail "decoder unable to decode byte sequence"
                }
                |> ParseResult.liftTask
