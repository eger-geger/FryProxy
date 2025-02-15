module FryProxy.IO.ByteBuffer

open System
open System.Text


/// <summary>
/// Find a region within a buffer and return <c>Some(first, last)</c> indexes
/// of the first matched region or <c>None</c>, if not found.
/// </summary>
/// <exception cref="ArgumentException"> Query array is empty. </exception>
let tryFindRange (query: byte array) (buff: byte ReadOnlyMemory) =
    if Array.isEmpty query then
        invalidArg (nameof query) "Empty query sequence"

    buff.ToArray()
    |> Array.windowed query.Length
    |> Array.tryFindIndex((=) query)
    |> Option.map(fun i -> struct (i, i + query.Length - 1))


/// <summary>
/// Return a slice from buffer start till the first inclusion the suffix,
/// including the suffix itself, or <c>None</c>, if suffix was not found.
/// </summary>
/// <exception cref="ArgumentException">Suffix is empty.</exception>
let tryTakeSuffix (suffix: byte array) buff =
    tryFindRange suffix buff |> Option.map(fun struct (_, r) -> buff.Slice(0, r))


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