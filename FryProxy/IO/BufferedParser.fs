module FryProxy.IO.BufferedParser

open System.IO
open System.Threading.Tasks
open FryProxy.IO
open Microsoft.FSharp.Core

type 'a Parser = ReadStreamBuffer * Stream -> 'a option Task

/// Parser evaluating to a constant value.
let unit a : 'a Parser = fun _ -> Task.FromResult(Some a)

/// <summary>
/// Create a parser consuming buffered bytes on each successful read.
/// </summary>
/// <param name="parseBytes">
/// Byte parsing function which is given reader buffer and returns
/// an optional tuple of consumed bytes count and parsed value.
/// </param>
let parseBuffer parseBytes : 'a Parser =
    fun (buff, stream) ->
        let consumeBytes (n, a) =
            buff.Discard n
            a

        task {
            let! span = buff.PickSpan stream

            return span.ToArray() |> parseBytes |> Option.map consumeBytes
        }

/// Parses a UTF8 encoded line terminated with a line break.
let parseUTF8Line: string Parser = parseBuffer ByteBuffer.tryTakeUTF8Line

/// <summary>
/// Execute sub-parser repeatedly as long as it succeeds and return results as list.
/// The resulting parser fails unless sub-parser has succeeded at least once.
/// </summary>
let eager (parser: 'a Parser) : 'a list Parser =
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

/// Transform value inside parser.
let map fn (parser: 'a Parser) : 'b Parser =
    fun state ->
        task {
            let! opt = parser state
            return Option.map fn opt
        }

/// Unwrap parsed value option, failing parser when empty.
let flatOpt (parser: 'a option Parser) : 'a Parser =
    fun state ->
        task {
            let! opt = parser state
            return Option.flatten opt
        }

/// Execute parsers sequentially and combine results with a binary function.
let map2 binOp (first: 'a Parser) (second: 'b Parser) : 'c Parser =
    fun state ->
        task {
            let! a = first state
            let! b = second state

            return Option.map2 binOp a b
        }

/// Execute fallback parser if the main fails.
let orElse (fallback: 'a Parser) (main: 'a Parser) =
    fun state ->
        task {
            let! opt = main state

            if Option.isSome opt then return opt else return! fallback state
        }
