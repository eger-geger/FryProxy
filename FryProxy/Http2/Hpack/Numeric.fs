namespace FryProxy.Http2.Hpack

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

    [<Literal>]
    let capacity = 6

    let zero = U8 0u

    let inline octetCap prefix =
        if prefix > 7 then 0uy else 255uy >>> prefix

    /// Create numeric literal from 32 bit value.
    let inline create value =
        if value <= 0xffu then U8 value
        elif value <= 0xffffu then U16 value
        else U32 value

    /// Attempt to extract 8-bit number from numeric literal.
    /// Fail with error when literal size exceeds 8 bits.
    let toUint8 num =
        match num with
        | U8 n -> Ok(uint8 n)
        | _ -> Error "numeric literal size exceeds 8 bits"

    /// Attempt to extract 16-bit number from numeric literal.
    /// Fail with error when literal size exceeds 16 bits.
    let toUint16 num =
        match num with
        | U8 n -> Ok(uint16 n)
        | U16 n -> Ok(uint16 n)
        | _ -> Error "numeric literal size exceeds 16 bits"

    /// Extract wrapped 32-bit number.
    let toUint32 num =
        match num with
        | U8 n -> n
        | U16 n -> n
        | U32 n -> n

    /// Encode numeric literal suffix.
    [<TailCall>]
    let rec encodeSuffix (stack: byte Span) i n =
        if n < 128u then
            stack[i] <- byte n
            i + 1
        else
            stack[i] <- 128uy ||| (byte n)
            encodeSuffix stack (i + 1) (n >>> 7)

    /// <summary>
    /// Encodes an unsigned integer as an octet sequence according to HPACK integer representation (RFC 7541 §5.1 / RFC 9113 §4.1).
    /// </summary>
    /// <param name="prefix">
    /// Number of high-order bits in the first octet reserved for flags or opcodes (i.e. 8 - N, where N is the prefix bit length).
    /// For example, 1 for a 7-bit prefix (capacity 127), 3 for a 5-bit prefix (capacity 31), or 4 for a 4-bit prefix (capacity 15).
    /// </param>
    /// <param name="n">The 32-bit unsigned integer value to encode.</param>
    /// <param name="buf">The destination byte span where encoded octets are written.</param>
    /// <returns>The total number of bytes written to <paramref name="buf"/> (between 1 and 6 bytes for a 32-bit value).</returns>
    /// <remarks>
    /// If <paramref name="n"/> is strictly less than 2^(8 - prefix) - 1, the value is placed directly into the low (8 - prefix) bits of the first octet.
    /// Otherwise, the prefix bits are set to all 1s (the maximum value) and the remainder (n - capacity) is encoded across subsequent octets
    /// as a variable-length integer using 7 bits per octet with the most significant bit (0x80) set as a continuation flag.
    /// The reserved high-order bits of the first octet are left as 0 so callers can bitwise OR flags or opcodes into <paramref name="buf"/>[0].
    /// </remarks>
    let inline encode prefix n (buf: byte Span) =
        let cap = octetCap prefix

        if n < uint32 cap then
            buf[0] <- (byte n)
            1
        else
            buf[0] <- cap
            encodeSuffix buf 1 (n - uint32 cap)

    [<TailCall>]
    let rec private decodeSuffix count num bytes =
        match Decoder.take bytes with
        | Ok b, _ ->
            let num' = num + (uint32 (b &&& 127uy) <<< int (count * 7u))

            if num' < num then
                DecoderResult(Error "numeric literal size exceeds 32 bits", count + 1u)
            elif 128uy > b then
                DecoderResult(Ok(create num'), count + 1u)
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
