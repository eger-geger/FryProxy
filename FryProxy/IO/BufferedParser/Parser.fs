module FryProxy.IO.BufferedParser.Parser

open System
open System.Threading.Tasks
open FryProxy.IO
open Microsoft.FSharp.Core


/// Parser evaluating to a constant value.
let unit a : 'a Parser = fun _ -> Task.FromResult(Some(0, a))

/// Failed parser.
let failed: 'a Parser = fun _ -> Task.FromResult None

/// Discard bytes consumed by parser when it succeeds.
let commit (parser: 'a Parser) : 'a Parser =
    fun (buff, stream) ->
        task {
            match! parser (buff, stream) with
            | Some(0, a) -> return Some(0, a)
            | Some(c, a) ->
                buff.Discard c
                return Some(0, a)
            | None -> return None
        }

/// Commit and execute parser, returning parsed value.
let run (parser: 'a Parser) state : 'a option Task =
    task {
        let! opt = commit parser state

        return opt |> Option.map snd
    }

/// Transform value inside parser.
let map fn (parser: 'a Parser) : 'b Parser =
    fun state ->
        task {
            match! parser state with
            | Some(c, a) -> return Some(c, fn a)
            | None -> return None
        }

/// Unwrap parsed value option, failing parser when empty.
let flatmap fn (parser: 'a Parser) : 'b Parser =
    fun state ->
        task {
            match! parser state with
            | Some(c, a) -> return a |> fn |> Option.map (fun b -> c, b)
            | None -> return None
        }

/// Execute parsers sequentially
let bind (binder: 'a -> 'b Parser) (parser: 'a Parser) : 'b Parser =
    fun state ->
        task {
            match! parser state with
            | Some(c, a) ->
                match! binder a state with
                | Some(c', b) -> return Some(c + c', b)
                | None -> return None
            | None -> return None
        }

/// Commit sub-parser repeatedly as long as it succeeds and return results as list.
let eager (parser: 'a Parser) : 'a list Parser =
    fun state ->
        task {
            let mutable loop = true
            let mutable size = 0
            let mutable results = List.empty

            while loop do
                match! commit parser state with
                | Some(s, a) ->
                    size <- size + s
                    results <- a :: results
                | None -> loop <- false

            return Some(size, List.rev results)
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
        task {
            let! span = buff.PickSpan stream

            return span.ToArray() |> parseBytes
        }



/// Parses a UTF8 encoded line terminated with a line break.
let parseUTF8Line: string Parser = parseBuffer ByteBuffer.tryTakeUTF8Line
