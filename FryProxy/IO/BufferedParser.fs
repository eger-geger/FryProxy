module FryProxy.IO.BufferedParser

open System.IO
open System.Threading.Tasks
open FryProxy.IO
open FryProxy.IO.ReadStreamBuffer
open Microsoft.FSharp.Core

type 'a BufferedParser = ReadStreamBuffer * Stream -> 'a option Task

let unit a : 'a BufferedParser = fun _ -> Task.FromResult(Some a)

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

/// Parses a UTF8 encoded line terminated with a line break.
let parseUTF8Line: string BufferedParser = parseBuffer ByteBuffer.tryTakeUTF8Line

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
