namespace FryProxy.Http.Hpack

#nowarn "0064"

open System
open Microsoft.FSharp.Core

/// Unsigned number binary representation.
[<Struct>]
type NumericLit =
    | U8 of uint32
    | U16 of uint32
    | U32 of uint32

module NumericLit =

    let zero = U8 0u

    let inline octetCap prefix =
        if prefix > 7 then 0uy else 255uy >>> prefix

    let inline create value =
        if value <= 0xffu then U8 value
        elif value <= 0xffffu then U16 value
        else U32 value

    let toUint8 num =
        match num with
        | U8 n -> Ok(uint8 n)
        | _ -> Error "numeric literal size exceeds 8 bits"

    let toUint16 num =
        match num with
        | U8 n -> Ok(uint16 n)
        | U16 n -> Ok(uint16 n)
        | _ -> Error "numeric literal size exceeds 16 bits"

    let toUint32 num =
        match num with
        | U8 n -> n
        | U16 n -> n
        | U32 n -> n

    /// Encode numeric literal suffix.
    [<TailCall>]
    let rec encodeSuffix (stack: byte Span) i n =
        if n < 128UL then
            stack[i] <- byte n
            i + 1
        else
            stack[i] <- 128uy ||| (byte n)
            encodeSuffix stack (i + 1) (n >>> 7)

    /// Encode numeric value as octet sequence within a buffer and return length.
    let inline encode prefix n (buf: byte Span) =
        let cap = octetCap prefix

        if n < uint64 cap then
            buf[0] <- (byte n)
            1
        else
            buf[0] <- cap
            encodeSuffix buf 1 (n - uint64 cap)

    [<TailCall>]
    let rec private decodeSuffix count num bytes =
        match Decoder.take bytes with
        | Ok b, _ ->
            let num' = num + (uint32(b &&& 127uy) <<< int(count * 7u))

            if num' < num then
                DecoderResult(Error "numeric literal size exceeds 32 bits", count + 1u)
            elif 128uy > b then
                DecoderResult(Ok(create num'), count)
            else
                decodeSuffix (count + 1u) num' (bytes.Slice(1))
        | Error e, n -> Error e, n

    /// Decode numeric value from octet sequence ignoring given number of bits in first octet.
    let decode prefix =
        let cap = octetCap prefix

        decoder {
            let! b = Decoder.take
            let prefix = cap &&& b

            if prefix < cap then
                return U8(uint32 prefix)
            else
                return! decodeSuffix 0u (uint32 prefix)
        }
