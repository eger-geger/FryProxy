module FryProxy.IO.ByteBuffer

open System
open System.Text

let empty = MemoryByteSeq()

/// Attempt to find inclusion position of a byte sequence within a memory region.
let rec tryFindSlice (query: byte ReadOnlySpan) (buff: byte ReadOnlyMemory) start =
    if start >= buff.Length then
        ValueNone
    else if buff.Slice(start, query.Length).Span.SequenceEqual(query) then
        ValueSome start
    else
        tryFindSlice query buff (start + 1)


/// Attempt to find and return the shortest memory prefix (leading sequence of bytes) ending with a given suffix.
let tryTakePrefix (suffix: byte ReadOnlySpan) buff =
    if suffix.IsEmpty then
        invalidArg (nameof suffix) "Empty suffix"

    let suffixLen = suffix.Length

    tryFindSlice suffix buff 0
    |> ValueOption.map(fun start -> buff.Slice(0, start + suffixLen))


/// Attempt to find and return the first line encoded in memory. Return nothing if it contains no line breaks.
let tryTakeLine (enc: Encoding) buff =
    let lb = ReadOnlySpan(enc.GetBytes "\n")

    tryTakePrefix lb buff
    |> ValueOption.map(fun suffix -> struct (uint16 suffix.Length, enc.GetString(suffix.Span)))


/// Attempt to decode leading buffer bytes as UTF8 line.
let tryTakeUTF8Line buf = tryTakeLine Encoding.UTF8 buf
