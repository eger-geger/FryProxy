module FryProxy.Http.Hpack.StringLit

open System
open Microsoft.FSharp.Core

[<Literal>]
let HuffmanEncodedFlag = 0b1000_0000uy

/// Decode ASCII string of given length.
let inline decodeRaw len =
    decoder {
        let! arr = Decoder.takeN len
        return String([| for c in arr -> char c |])
    }

/// Decode Huffman byte sequence of given length.
let inline decodeHuf len =
    decoder {
        let! arr = Decoder.takeN len

        match Huffman.decodeStr arr with
        | Ok s -> return s
        | Error e -> return! Decoder.error e
    }

/// Decode ASCII string literal.
let decode =
    decoder {
        let! first = Decoder.peek

        let decoder =
            if first |> Flag.check HuffmanEncodedFlag then
                decodeHuf
            else
                decodeRaw

        let! lenLit = NumericLit.decode 1

        match NumericLit.toUint16 lenLit with
        | Ok len -> return! decoder len
        | Error msg -> return! Decoder.error msg
    }
