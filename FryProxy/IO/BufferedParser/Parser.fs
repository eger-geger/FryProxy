module FryProxy.IO.BufferedParser.Parser

open System.Threading.Tasks
open FryProxy.IO


/// Parser evaluating to a constant value.
let unit a : 'a Parser = fun _ -> Task.FromResult(Some a)

/// Failed parser.
let failed: 'a Parser = fun _ -> Task.FromResult None

/// Transform value inside parser.
let map fn (parser: 'a Parser) : 'b Parser =
    fun state ->
        task {
            let! opt = parser state
            return Option.map fn opt
        }

/// Unwrap parsed value option, failing parser when empty.
let flatmap fn (parser: 'a Parser) : 'b Parser =
    fun state ->
        task {
            let! opt = parser state

            return opt |> Option.map fn |> Option.flatten
        }

/// Execute parsers sequentially
let bind (binder: 'a -> 'b Parser) (parser: 'a Parser) : 'b Parser =
    fun state ->
        task {
            match! parser state with
            | Some(a) -> return! binder a state
            | None -> return None
        }

/// Execute sub-parser repeatedly as long as it succeeds and return results as list.
let eager (parser: 'a Parser) : 'a list Parser =
    fun state ->
        task {
            let mutable loop = true
            let mutable results = List.empty

            while loop do
                match! parser state with
                | Some(a) -> results <- a :: results
                | None -> loop <- false

            return Some(List.rev results)
        }


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
