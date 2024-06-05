module FryProxy.IO.BufferedParser.Parser

open System
open System.Collections.Generic
open FryProxy.IO
open ParseResult

/// Parser evaluating to a constant value.
let inline unit a : Parser<'a> = fun (_, s) -> unit(s, a)

/// Failed parser.
let inline failed reason : Parser<'a> = fun _ -> error reason

/// Execute parsers sequentially.
let inline bind (binder: 'a -> Parser<'b>) (parser: Parser<'a>) : Parser<'b> =
    fun (rb, s) ->
        liftTask
        <| task {
            let! s', a = parser(rb, s)
            return! binder a (rb, s')
        }

/// Transform value inside parser.
let inline map fn (parser: Parser<'a>) : Parser<'b> =
    parser >> map(fun (s', a) -> (s', fn a))

/// Ignore parsed value.
let inline ignore p = map ignore p

/// Unwrap parsed value option, failing parser when empty.
let inline flatmap (fn: 'a -> 'b Option) (parser: Parser<'a>) : Parser<'b> =
    parser
    >> ParseResult.bind(fun (s, a) ->
        match fn a with
        | Some b -> ParseResult.unit(s, b)
        | None -> error $"failed {typeof<'a>} -> {typeof<'b>}")

/// Fail the parser unless parsed value satisfies given condition.
let inline must msg cond parser : Parser<_> =
    let validate (s, a) =
        if cond a then
            ParseResult.unit(s, a)
        else
            error $"{a} is not {msg}"

    parser >> ParseResult.bind validate

/// Discard bytes consumed by parser when it succeeds.
let inline commit (parser: Parser<'a>) : Parser<'a> =
    fun (rb, state) ->
        parser(rb, state)
        |> ParseResult.map(fun (s', a) ->
            match s' with
            | Running { Offset = 0us } -> s', a
            | Running { Offset = lo } -> let _ = rb.Discard(int lo) in Running { Offset = 0us }, a
            | Yielded _ as ls -> ls, a)

/// Commit and execute parser, returning parsed value.
let inline run rb (parser: Parser<'a>) =
    (rb, Running { Offset = 0us }) |> commit parser |> ParseResult.map snd

/// Commit sub-parser repeatedly as long as it succeeds and return results as list.
let inline eager (parser: Parser<'a>) : Parser<'a list> =
    fun (rb, s) ->
        liftTask
        <| task {
            let mutable state = s
            let mutable proceed = true
            let mutable xs = List.empty

            while proceed do
                try
                    let! s', x = parser(rb, state)
                    state <- s'
                    xs <- x :: xs
                with :? ParseError ->
                    proceed <- false

            return state, List.rev xs
        }

/// Force lazy parser evaluation, failing if not yet completed.
let inline unyielding (parser: 'a StrictParser) : 'a Parser =
    fun (rb, s) ->
        match s with
        | Running state -> parser(rb, state)
        | Yielded x when x.Consumed -> parser(rb, { Offset = 0us })
        | Yielded x -> error $"{x} has not been consumed yet"

/// Lazy parser evaluating to a raw buffer content.
let inline bytes (n: uint64) : Parser<IByteBuffer> =
    unyielding
    <| fun (rb, _) ->
        let span = BufferSpan(rb, n)
        ParseResult.unit(Yielded span, span)

/// Lazy parser evaluating to another parser to produce a sequence.
let inline unfold (gen: 'a LazySeqGen) : 'a IAsyncEnumerable Parser =
    unyielding
    <| fun (rb, state) ->
        let iter = LazyIter(gen, rb, Running state)
        ParseResult.unit(Yielded iter, iter.ToEnumerable())

/// <summary>
/// Create a parser consuming buffered bytes on each successful read.
/// </summary>
/// <param name="decode">
/// Byte parsing function which is given reader buffer and returns
/// an optional tuple of consumed bytes count and parsed value.
/// </param>
let decoder decode : Parser<'a> =
    let tryDecode offset (mem: ReadOnlyMemory<byte>) =
        mem.Slice(int offset).ToArray()
        |> decode
        |> Option.map(fun (n, v) -> Running { Offset = offset + n }, v)

    let fail cause =
        error $"Decoding {typeof<'a>} failed: {cause}"

    unyielding
    <| fun (rb, { Offset = offset }) ->
        if int offset > rb.Capacity then
            fail "offset beyond capacity"
        else
            match tryDecode offset rb.Pending with
            | Some(s, x) -> ParseResult.unit(s, x)
            | None ->
                liftTask
                <| task {
                    let! pending = rb.Pick()

                    match tryDecode offset pending with
                    | Some(s, x) -> return (s, x)
                    | None -> return! fail "decoder unable to decode byte sequence"
                }
