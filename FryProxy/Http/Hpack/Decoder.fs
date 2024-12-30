namespace FryProxy.Http.Hpack

open System

/// Decoded value or error paired with number of consumed octets.
type 'a DecoderResult = (struct (Result<'a, string> * uint32))

/// Decode leading sequence of octets.
type 'a Decoder = delegate of byte ReadOnlySpan -> 'a DecoderResult

module Decoder =
    
    /// Evaluate to constant value without consuming any bytes.
    let inline unit value = Decoder(fun _ -> Ok value, 0u)

    /// Evaluate to decoding failure without consuming any bytes.
    let inline error msg = Decoder(fun _ -> Error msg, 0u)
    
    /// Evaluate decoder on read-only span of bytes.
    let inline run (decoder: _ Decoder) bytes =
        let struct (res, _) = decoder.Invoke bytes
        res
    
    /// Evaluate decoder on byte array.
    let inline runArr decoder (arr: byte array) = run decoder (ReadOnlySpan(arr))
    
    /// Combine decoders sequentially.
    let inline bind ([<InlineIfLambda>] fn: 'a -> 'b Decoder) (decoder: 'a Decoder) bytes =
        match decoder.Invoke bytes with
        | Ok a, n ->
            let struct (res, m) = (fn a).Invoke(bytes.Slice(int n))
            DecoderResult(res, m + n)
        | Error e, n -> DecoderResult(Error e, n)
    
    /// Transform decoder value.
    let inline map ([<InlineIfLambda>] fn: 'a -> 'b) (decoder: 'a Decoder) bytes =
        match decoder.Invoke bytes with
        | Ok a, n -> Ok(fn a), n
        | Error e, n -> Error e, n
    
    /// Peek into next byte without consuming it.
    let inline peek (bytes: byte ReadOnlySpan) =
        if bytes.IsEmpty then
            DecoderResult(Error "unexpected end of sequence", 0u)
        else
            DecoderResult(Ok bytes[0], 0u)
    
    /// Consume a single octet.
    let inline take (bytes: byte ReadOnlySpan) =
        if bytes.IsEmpty then
            DecoderResult(Error "unexpected end of sequence", 0u)
        else
            DecoderResult(Ok bytes[0], 1u)
    
    /// Consume given number of octets.
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
