module FryProxy.IO.BufferedParser.Parser

open System.Threading.Tasks
open Microsoft.FSharp.Core


/// Parser evaluating to a constant value.
let unit a : 'a Parser =
    fun (_, _, c) -> Task.FromResult(Some(c, a))

/// Failed parser.
let failed: 'a Parser = fun _ -> Task.FromResult None

/// Discard bytes consumed by parser when it succeeds.
let commit (parser: 'a Parser) : 'a Parser =
    fun (buff, stream, c) ->
        task {
            match! parser (buff, stream, c) with
            | Some(0, a) -> return Some(0, a)
            | Some(c, a) ->
                buff.Discard c
                return Some(0, a)
            | None -> return None
        }

/// Commit and execute parser, returning parsed value.
let run (parser: 'a Parser) (buff, is) : 'a option Task =
    task {
        let! opt = commit parser (buff, is, 0)

        return opt |> Option.map snd
    }

/// Transform value inside parser.
let map fn (parser: 'a Parser) : 'b Parser =
    fun state ->
        task {
            let! opt = parser state

            return opt |> Option.map (fun (a, b) -> a, fn b)
        }

/// Unwrap parsed value option, failing parser when empty.
let flatmap fn (parser: 'a Parser) : 'b Parser =
    fun state ->
        task {
            match! map fn parser state with
            | Some(c, opt) -> return Option.map (fun b -> c, b) opt
            | None -> return None
        }

/// Execute parsers sequentially
let bind (binder: 'a -> 'b Parser) (parser: 'a Parser) : 'b Parser =
    fun (buff, is, c) ->
        task {
            match! parser (buff, is, c) with
            | Some(c, a) -> return! binder a (buff, is, c)
            | None -> return None
        }

/// Commit sub-parser repeatedly as long as it succeeds and return results as list.
let eager (parser: 'a Parser) : 'a list Parser =
    fun (buff, is, c) ->
        task {
            let mutable loop = true
            let mutable size = c
            let mutable results = List.empty

            while loop do
                match! parser (buff, is, size) with
                | Some(c, a) ->
                    size <- size + c
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
    fun (buff, stream, c) ->
        let parsedPending =
            let bytes = buff.Pending.ToArray()
            if bytes.Length > c then Some(parseBytes bytes[c..]) else None

        match parsedPending with
        | Some v -> Task.FromResult v
        | None ->
            task {
                let! span = buff.PickSpan stream
                return span.ToArray()[c..] |> parseBytes
            }
