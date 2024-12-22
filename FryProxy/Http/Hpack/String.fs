module FryProxy.Http.Hpack.StringLit

open System
open System.Text
open Microsoft.FSharp.Core

[<Literal>]
let HuffmanEncodedFlag = 0b1000_0000uy

[<Literal>]
let MaxLength = 0xffff

/// Encode ASCII string as raw literal.
let inline encodeRaw (str: string) (buf: byte Span) =
    let len = NumericLit.encode 1 (uint64 str.Length) buf
    Encoding.ASCII.GetBytes(str, buf.Slice(len)) + len

/// Encode extended ASCII string using static Huffman table.
let inline encodeHuf (str: string) (buf: byte Span) =
    let sLen = Huffman.encodeStr str (buf.Slice(9))
    let nLen = NumericLit.encode 1 (uint64 sLen) buf
    let total = sLen + nLen
    
    buf.Slice(9, sLen).CopyTo(buf.Slice(nLen))
    buf[0] <- buf[0] ||| HuffmanEncodedFlag
    buf.Slice(total, 9 - nLen).Clear()
    total

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
