namespace FryProxy.Http.Hpack

#nowarn "0064"

open System
open FryProxy.Extension
open Microsoft.FSharp.Core

/// Unsigned number binary representation.
[<Struct>]
type NumericLit =
    | U8 of uint64
    | U16 of uint64
    | U32 of uint64
    | U64 of uint64

module NumericLit =

    let zero = U8 0UL

    let inline octetCap prefix =
        if prefix > 7 then 0uy else 255uy >>> prefix

    let inline create value =
        if value <= 0xffUL then U8 value
        elif value <= 0xffffUL then U16 value
        elif value <= 0xffff_ffffUL then U32 value
        else U64 value

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
        | U8 n -> Ok(uint32 n)
        | U16 n -> Ok(uint32 n)
        | U32 n -> Ok(uint32 n)
        | _ -> Error "numeric literal size exceeds 32 bits"

    let toUint64 num =
        match num with
        | U8 n -> n
        | U16 n -> n
        | U32 n -> n
        | U64 n -> n

    [<TailCall>]
    let rec encodeSuffix (stack: byte Span) i n =
        if n < 128UL then
            stack[i] <- byte n
            stack.Slice(0, (i + 1))
        else
            stack[i] <- 128uy ||| (byte n)
            encodeSuffix stack (i + 1) (n >>> 7)

    /// Encode numeric value to binary octet sequence allocated on the stack.
    let inline encode prefix num =
        let n = toUint64 num
        let cap = octetCap prefix
        let stack = Stackalloc.span 9

        if n < uint64 cap then
            stack[0] <- byte n
            stack.Slice(0, 1)
        else
            stack[0] <- cap
            encodeSuffix stack 1 (n - uint64 cap)

    [<TailCall>]
    let rec private decodeSuffix octets num bytes =
        match Decoder.take bytes with
        | Ok b, _ ->
            let num' = num + (uint64(b &&& 127uy) <<< int(octets * 7us))

            if 128uy > b then
                DecoderResult(Ok(create num'), octets)
            else
                decodeSuffix (octets + 1us) num' (bytes.Slice(1))
        | Error e, n -> Error e, n

    /// Decode numeric value from octet sequence ignoring given number of bits in first octet.
    let decode prefix =
        let cap = octetCap prefix

        decoder {
            let! b = Decoder.take
            let prefix = cap &&& b

            if prefix < cap then
                return U8(uint64 prefix)
            else
                return! decodeSuffix 0us (uint64 prefix)
        }
