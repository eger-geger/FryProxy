module FryProxy.Http.Hpack.StringLit

open System
open Microsoft.FSharp.Core

/// Decode ASCII string of given length.
let decodeRaw (len: uint16) =
    decoder {
        let! arr = Decoder.takeArr(int len)
        return arr |> Array.map char |> String
    }

/// Decode Huffman byte sequence of given length.
let decodeHuf (len: uint16) i (bs: byte array) =
    let j = i + int len - 1

    match Huffman.decodeStr bs[i..j] with
    | Ok s -> DecVal(s, j + 1)
    | Error s -> DecErr(s, j)

/// Decode ASCII string literal.
let decode =
    decoder {
        let! first = Decoder.peek

        let decoder =
            if (first &&& 128uy) = 128uy then
                decodeHuf
            else
                decodeRaw

        let! lenLit = NumericLit.decode 1

        match NumericLit.uint16 lenLit with
        | Ok len -> return! decoder len
        | Error msg -> return! Decoder.error msg
    }
