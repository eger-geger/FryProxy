module FryProxy.IO.BufferedParser

open System
open System.IO
open System.Text
open System.Threading.Tasks
open FryProxy.IO.ReadStreamBuffer
open Microsoft.FSharp.Core

type 'a BufferedParser = ReadStreamBuffer * Stream -> 'a option Task

/// <summary>
/// Confirm that byte buffer starts with a prefix.
/// </summary>
/// <returns>Prefix length or None.</returns>
let tryPrefix (prefix: byte array) (buff: byte array) =
    if Array.isEmpty prefix then
        invalidArg (nameof prefix) "Expected array is empty"

    if prefix.Length > buff.Length then
        ArgumentOutOfRangeException(nameof prefix, prefix.Length, "Expected array size exceeds buffer capacity")
        |> raise

    if prefix = buff[.. prefix.Length] then Some(prefix.Length) else None

/// <summary>
/// Find the first inclusion of a byte sequence within a buffer.
/// </summary>
/// <param name="query">Sequence of interest.</param>
/// <param name="buff">Buffer.</param>
/// <returns>Optional tuple of sequence bounding indexes within a buffer.</returns>
let tryFind (query: byte array) (buff: byte array) =
    if Array.isEmpty query then
        invalidArg (nameof query) "Empty query sequence"

    buff
    |> Array.windowed query.Length
    |> Array.tryFindIndex ((=) query)
    |> Option.map (fun i -> i, i + query.Length - 1)


/// <summary>
/// Extract buffer leading byte sequence ending with a suffix.
/// </summary>
/// <param name="suffix">Returned sequence suffix.</param>
/// <param name="buff">Buffer.</param>
/// <returns>
/// Bytes in range from buffer start to first suffix location in buffer (inclusive) or None, if not found.
/// </returns>
let trySuffix (suffix: byte array) (buff: byte array) =
    tryFind suffix buff |> Option.map (fun (_, r) -> buff[..r])


/// <summary>
/// Consume a line of text from byte buffer.
/// </summary>
/// <param name="enc">Text encoding.</param>
/// <returns>
/// Optional tuple of number of consumed bytes and resulting string including final line break.
/// </returns>
let tryLine (enc: Encoding) =
    let suffix = enc.GetBytes "\n"

    trySuffix suffix >> Option.map (fun b -> b.Length, enc.GetString(b))


let constant a : 'a BufferedParser = fun _ -> Task.FromResult(Some a)

/// <summary>
/// Create a parser consuming buffered bytes on each successful read.
/// </summary>
/// <param name="parseBytes">
/// Byte parsing function which is given reader buffer and returns
/// an optional tuple of consumed bytes count and parsed value.
/// </param>
let parseBuffer parseBytes : 'a BufferedParser =
    fun (buff, stream) ->
        let consumeBytes (n, a) =
            buff.discard n
            a

        task {
            let! span = buff.pickSpan stream

            return span.ToArray() |> parseBytes |> Option.map consumeBytes
        }

/// <summary> Parses a line of UTF8 encoded bytes from buffered stream. </summary>
let parseUTF8Line: string BufferedParser = parseBuffer (tryLine Encoding.UTF8)

/// <summary>
/// Execute sub-parser repeatedly as long as it succeeds and return a list of its results.
/// The resulting parser is successful if sub-parser has succeeded at least once.
/// </summary>
let eager (parser: 'a BufferedParser) : 'a list BufferedParser =
    fun state ->
        task {
            let mutable loop = true
            let mutable results = List.Empty

            while loop do
                match! parser state with
                | Some(a) -> results <- a :: results
                | None -> loop <- false

            return if results.IsEmpty then None else Some(List.rev results)
        }

/// <summary> Apply function to parsed value. </summary>
let map fn (parser: 'a BufferedParser) : 'b BufferedParser =
    fun state ->
        task {
            let! opt = parser state
            return Option.map fn opt
        }

/// <summary> Unwrap parsed value option, failing parser when empty. </summary>
let flatOpt (parser: 'a option BufferedParser) : 'a BufferedParser =
    fun state ->
        task {
            let! opt = parser state
            return Option.flatten opt
        }

/// <summary>
/// Combine results of a 2 parsers evaluated subsequently.
/// </summary>
/// <param name="binOp">Binary operation applied to parser results.</param>
/// <param name="first">Parser evaluated first/</param>
/// <param name="second">Parser evaluated second.</param>
let join binOp (first: 'a BufferedParser) (second: 'b BufferedParser) : 'c BufferedParser =
    fun state ->
        task {
            let! a = first state
            let! b = second state

            return Option.map2 binOp a b
        }

/// <summary> Use fallback parser if main fails. </summary>
let orElse (fallback: 'a BufferedParser) (main: 'a BufferedParser) =
    fun state ->
        task {
            let! opt = main state

            if Option.isSome opt then return opt else return! fallback state
        }
