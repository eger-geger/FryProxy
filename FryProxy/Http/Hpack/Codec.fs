module FryProxy.Http.Hpack.Codec

open System

let inline byteCap offset = 255uy >>> offset

/// Encode numeric value into octet sequence skipping given number of bits within first octet.
let encodeInt offset n =
    let rec loop n acc =
        if n < 128UL then
            byte n :: acc |> List.rev
        else
            (128uy ||| byte(n % 128UL)) :: acc |> loop(n / 128UL)

    let bytes =
        let cap = byteCap offset |> uint64

        if n < cap then
            [| byte n |]
        else
            [ byte cap ] |> loop(n - cap) |> List.toArray

    Span(bytes)

/// Decode numeric value from octet sequence ignoring given number of bits in first octet.
let decodeInt offset (bytes: byte seq) =
    let cap = byteCap offset
    use is = (Seq.indexed bytes).GetEnumerator()

    let inline next () =
        if is.MoveNext() then
            is.Current
        else
            invalidArg (nameof bytes) "incomplete byte sequence"

    let rec loop size acc =
        let i, n = next()
        let carry = n &&& 128uy
        let acc' = acc + (uint64(n ^^^ carry) <<< 7 * (i - 1))

        if size > 64 then
            raise(OverflowException $"number size exceeds uint64: {size}")
        elif carry = 0uy then
            acc'
        else
            acc' |> loop(size + 7)

    let _, first = next()
    let prefix = cap &&& first

    if prefix < cap then
        uint64 prefix
    else
        uint64 cap |> loop(8 - offset)
