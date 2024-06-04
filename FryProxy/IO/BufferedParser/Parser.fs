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
    fun (rb, s) ->
        parser(rb, s)
        |> ParseResult.map(fun (s', a) ->
            match s' with
            | { Offset = 0us } -> s', a
            | { Offset = lo } ->
                rb.Discard(int lo)
                { s' with Offset = 0us }, a)

/// Commit and execute parser, returning parsed value.
let inline run rb (parser: Parser<'a>) =
    (rb, ParseState.Zero) |> commit parser |> ParseResult.map snd

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
let inline strict (parser: 'a Parser) : 'a Parser =
    fun (rb, s) ->
        match s with
        | { Mode = StrictMode } -> parser(rb, s)
        | { Mode = LazyMode x } when x.Consumed -> parser(rb, { s with Mode = StrictMode })
        | _ -> error "Parser has not finished yet"

/// Parser evaluating to a raw buffer content.
let inline bytes (n: uint64) : Parser<IByteBuffer> =
    strict
    <| fun (rb, _) ->
        let span = BufferSpan(rb, n)
        let state = { Offset = 0us; Mode = LazyMode(span) }
        ParseResult.unit(state, span)

let inline unfold (gen: 'a LazySeqGen) : 'a IAsyncEnumerable Parser =
    strict
    <| fun (rb, s) ->
        let iter = LazyIter(gen, rb, s)
        ParseResult.unit({ s with Offset = 0us; Mode = LazyMode(iter) }, iter.ToEnumerable())

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
            error $"Decoding {typeof<'a>} failed: {cause}"

        let tryDecode (mem: ReadOnlyMemory<byte>) =
            mem.Slice(offset).ToArray()
            |> decode
            |> Option.map(fun (n, v) -> { s with Offset = s.Offset + n }, v)

        if offset > rb.Capacity then
            fail "offset beyond capacity"
        else
            match tryDecode rb.Pending with
            | Some(s, x) -> ParseResult.unit(s, x)
            | None ->
                liftTask
                <| task {
                    let! pending = rb.Pick()

                    match tryDecode pending with
                    | Some(s, x) -> return (s, x)
                    | None -> return! fail "decoder unable to decode byte sequence"
                }
