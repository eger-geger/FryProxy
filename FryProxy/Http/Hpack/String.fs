module FryProxy.Http.Hpack.StringLit

open System
open System.Runtime.CompilerServices
open System.Text
open FryProxy.Extension
open Microsoft.FSharp.Core

[<Literal>]
let HuffmanEncodedFlag = 0b1000_0000uy

[<Literal>]
let MaxLength = 0xffff

[<Struct; IsByRefLike>]
type Octets = { Len: byte Span; Str: byte Span }

let toArray { Len = lenOct; Str = strOct } =
    Array.concat [ lenOct.ToArray(); strOct.ToArray() ]

/// Encode ASCII string as raw literal.
let inline encodeRaw (str: string) : Octets =
    let lenOct = NumericLit.encode 1 (U32(uint64 str.Length))
    let strOct = Stackalloc.medium(str.Length)
    Encoding.ASCII.GetBytes(str, strOct) |> ignore

    { Len = lenOct; Str = strOct }

let inline encodeHuf (str: string) : Octets =
    let strOct = Huffman.encodeStr str
    let lenOct = NumericLit.encode 1 (U32(uint64 strOct.Length))
    lenOct[0] <- lenOct[0] ||| HuffmanEncodedFlag

    { Len = lenOct; Str = strOct }

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
        let! lenLit = NumericLit.decode 1

        let decoder =
            if first |> Flag.check HuffmanEncodedFlag then
                decodeHuf
            else
                decodeRaw

        match NumericLit.toUint16 lenLit with
        | Ok len -> return! decoder len
        | Error msg -> return! Decoder.error msg
    }
