module FryProxy.IO.ByteBuffer

open System.Text


/// <summary>
/// Find a subarray within a buffer and return <c>Some(first, last)</c> indexes
/// of the first matched region or <c>None</c>, if not found.
/// </summary>
/// <exception cref="ArgumentException"> Query array is empty. </exception>
let tryFindRange (query: byte array) (buff: byte array) =
    if Array.isEmpty query then
        invalidArg (nameof query) "Empty query sequence"

    buff
    |> Array.windowed query.Length
    |> Array.tryFindIndex ((=) query)
    |> Option.map (fun i -> i, i + query.Length - 1)


/// <summary>
/// Return a subarray from buffer start till the first inclusion the suffix,
/// including the suffix itself, or <c>None</c>, if suffix was not found.
/// </summary>
/// <exception cref="ArgumentException">Suffix is empty.</exception>
let tryTakeSuffix (suffix: byte array) (buff: byte array) =
    tryFindRange suffix buff |> Option.map (fun (_, r) -> buff[..r])


/// <summary>
/// Decode bytes from buffer start till the first line break sequence.
/// </summary>
/// <param name="enc">Decodes string and determines line break sequence.</param>
/// <returns>
/// Decoded string following its byte size, or <c>None</c> when buffer does not contain a line break.
/// </returns>
let tryTakeLine (enc: Encoding) =
    let suffix = enc.GetBytes "\n"
    tryTakeSuffix suffix >> Option.map (fun b -> b.Length, enc.GetString(b))


/// Attempt to decode leading buffer bytes as UTF8 line.
let tryTakeUTF8Line = tryTakeLine Encoding.UTF8
