namespace FryProxy.Http.Hpack

open System

type 'a DecoderResult = (struct (Result<'a, string> * uint16))

type 'a Decoder = delegate of byte ReadOnlySpan -> 'a DecoderResult

module Decoder =

    let inline unit value = Decoder(fun _ -> Ok value, 0us)

    let inline error msg = Decoder(fun _ -> Error msg, 0us)

    let inline run (decoder: _ Decoder) bytes =
        let struct (res, _) = decoder.Invoke bytes
        res

    let inline runArr decoder (arr: byte array) = run decoder (ReadOnlySpan(arr))

    let inline bind ([<InlineIfLambda>] fn: 'a -> 'b Decoder) (decoder: 'a Decoder) bytes =
        match decoder.Invoke bytes with
        | Ok a, n ->
            let struct (res, m) = (fn a).Invoke(bytes.Slice(int n))
            DecoderResult(res, m + n)
        | Error e, n -> DecoderResult(Error e, n)

    let inline map ([<InlineIfLambda>] fn: 'a -> 'b) (decoder: 'a Decoder) bytes =
        match decoder.Invoke bytes with
        | Ok a, n -> Ok(fn a), n
        | Error e, n -> Error e, n

    let inline peek (bytes: byte ReadOnlySpan) =
        if bytes.IsEmpty then
            DecoderResult(Error "unexpected end of sequence", 0us)
        else
            DecoderResult(Ok bytes[0], 0us)

    let inline take (bytes: byte ReadOnlySpan) =
        if bytes.IsEmpty then
            DecoderResult(Error "unexpected end of sequence", 0us)
        else
            DecoderResult(Ok bytes[0], 1us)

    let inline takeN n (bytes: byte ReadOnlySpan) =
        if bytes.Length < int n then
            DecoderResult(Error $"insufficient number of bytes ({bytes.Length}) < {n}", n)
        else
            Ok(bytes.Slice(0, int n).ToArray()), n

type DecoderBuilder() =

    member inline _.Bind(decoder, binder) = Decoder(Decoder.bind binder decoder)

    member inline _.Return a = Decoder.unit a

    member inline _.ReturnFrom decoder = decoder

[<AutoOpen>]
module Global =
    let decoder = DecoderBuilder()
