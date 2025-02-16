module FryProxy.IO.ByteBuffer

open System
open System.Text


/// <summary>
/// Find a region within a buffer and return position and length
/// of the first matched region or <c>None</c>, if not found.
/// </summary>
/// <exception cref="ArgumentException"> Query array is empty. </exception>
let tryFindSlice (query: byte array) (buff: byte ReadOnlyMemory) =
    if Array.isEmpty query then
        invalidArg (nameof query) "Empty query sequence"

    buff.ToArray() |> Seq.windowed query.Length |> Seq.tryFindIndex((=) query)


/// <summary>
/// Return a slice from buffer start till the first inclusion the suffix,
/// including the suffix itself, or <c>None</c>, if suffix was not found.
/// </summary>
/// <exception cref="ArgumentException">Suffix is empty.</exception>
let tryTakeSuffix (suffix: byte array) buff =
    tryFindSlice suffix buff
    |> Option.map(fun start -> buff.Slice(0, start + suffix.Length))


/// <summary>
/// Decode bytes from buffer start till the first line break sequence.
/// </summary>
/// <param name="enc">Decodes string and determines line break sequence.</param>
/// <returns>
/// Decoded string following its byte size, or <c>None</c> when buffer does not contain a line break.
/// </returns>
let tryTakeLine (enc: Encoding) =
    let suffix = enc.GetBytes "\n"

    tryTakeSuffix suffix
    >> Option.map(fun b -> struct (uint16 b.Length, enc.GetString(b.Span)))


/// Attempt to decode leading buffer bytes as UTF8 line.
let tryTakeUTF8Line = tryTakeLine Encoding.UTF8
