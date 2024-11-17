module FryProxy.Http.Hpack.StringLit

open System
open Microsoft.FSharp.Core

/// Decode ASCII string of given length.
let decodeRaw (len: uint16) =
    decoder {
        let! arr = Decoder.takeArr(int len)
        return arr |> Array.map char |> String
    }

let decodeHof (len: uint16) i (bs: byte array) = DecVal("", i + int len)

/// Decode ASCII string literal.
let decode =
    decoder {
        let! first = Decoder.peek

        let decoder =
            if (first &&& 128uy) = 128uy then
                decodeHof
            else
                decodeRaw

        let! lenLit = NumericLit.decode 1

        match NumericLit.uint16 lenLit with
        | Ok len -> return! decoder len
        | Error msg -> return! Decoder.error msg
    }
