namespace FryProxy.Http.Hpack

#nowarn "0064"

open System
open Microsoft.FSharp.Core

/// Unsigned number binary representation.
[<Struct>]
type NumericLit = { Prefix: uint8; MSB: uint8 list }

module NumericLit =

    let zero = { Prefix = 0uy; MSB = List.Empty }

    let inline byteCap offset =
        if offset > 7 then 0uy else 255uy >>> offset

    /// Encode numeric value into octet sequence skipping given number of bits within first octet.
    let inline from ntp offset (n: ^a) =
        let n128 = ntp 128uy

        let rec loop m acc =
            if m < n128 then
                byte m :: acc
            else
                (128uy ||| byte(m % n128)) :: acc |> loop(m / n128)

        let cap = byteCap offset

        if n < ntp cap then
            { Prefix = byte n; MSB = List.Empty }
        else
            { Prefix = cap; MSB = [] |> loop(n - (ntp cap)) }

    let from8 = from uint8

    let from16 = from uint16

    let from32 = from uint32

    let from64 = from uint64

    let toList { Prefix = p; MSB = msb } = p :: (List.rev msb)

    let toArray = toList >> List.toArray

    let toSpan n = Span(toArray n)

    /// Convert numeric literal to unsigned integer or error in case of overflow.
    let inline toNum fn maxSize (nl: NumericLit) =
        let msb = List.rev nl.MSB

        let rec loop i size acc =
            if size > maxSize then
                Error($"numeric literal exceeded the size of {typeof<'a>.Name}: {toList nl}")
            elif i = msb.Length then
                Ok acc
            else
                acc + (fn(msb[i] &&& 127uy) <<< 7 * i) |> loop (i + 1) (size + 7uy)

        loop 0 0uy (fn nl.Prefix)

    let uint8 = toNum uint8 8uy

    let uint16 = toNum uint16 16uy
    
    let uint32 = toNum uint32 32uy
    
    let uint64 = toNum uint64 64uy

    [<TailCall>]
    let rec private decodeMSB acc j bs =
        match Decoder.take j bs with
        | DecVal(b, off) ->
            let acc' = b :: acc

            if b &&& 128uy = 0uy then
                DecVal(acc', off)
            else
                (off, bs) ||> decodeMSB acc'
        | DecErr(msg, off) -> DecErr(msg, off)

    /// Decode numeric value from octet sequence ignoring given number of bits in first octet.
    let decode offset =
        let cap = byteCap offset

        decoder {
            let! b = Decoder.take
            let prefix = cap &&& b

            if prefix < cap then
                return { Prefix = prefix; MSB = List.Empty }
            else
                let! msb = decodeMSB List.Empty
                return { Prefix = prefix; MSB = msb }
        }
