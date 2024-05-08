module FryProxy.IO.BufferedParser.Parser

open System.Threading.Tasks
open Microsoft.FSharp.Core


/// Parser evaluating to a constant value.
let unit a : Parser<'a, 's> =
    fun (_, c) -> Task.FromResult(Some(c, a))

/// Failed parser.
let failed: Parser<'a, 's> = fun _ -> Task.FromResult None

/// Discard bytes consumed by parser when it succeeds.
let commit (parser: Parser<'a, 's>) : Parser<'a, 's> =
    fun (buff, c) ->
        task {
            match! parser (buff, c) with
            | Some(0, a) -> return Some(0, a)
            | Some(c, a) ->
                buff.Discard c
                return Some(0, a)
            | None -> return None
        }

/// Commit and execute parser, returning parsed value.
let run (parser: Parser<'a, 's>) buff : 'a option Task =
    task {
        let! opt = commit parser (buff, 0)

        return opt |> Option.map snd
    }

/// Transform value inside parser.
let map fn (parser: Parser<'a, 's>) : Parser<'b, 's> =
    fun state ->
        task {
            let! opt = parser state

            return opt |> Option.map (fun (a, b) -> a, fn b)
        }

/// Unwrap parsed value option, failing parser when empty.
let flatmap fn (parser: Parser<'a, 's>) : Parser<'b, 's> =
    fun state ->
        task {
            match! map fn parser state with
            | Some(c, opt) -> return Option.map (fun b -> c, b) opt
            | None -> return None
        }

/// Execute parsers sequentially
let bind (binder: 'a -> Parser<'b, 's>) (parser: Parser<'a, 's>) : Parser<'b, 's> =
    fun (buff, c) ->
        task {
            match! parser (buff, c) with
            | Some(c, a) -> return! binder a (buff, c)
            | None -> return None
        }

/// Commit sub-parser repeatedly as long as it succeeds and return results as list.
let eager (parser: Parser<'a, 's>) : Parser<'a list, 's> =
    fun (buff, c) ->
        task {
            let mutable loop = true
            let mutable size = c
            let mutable results = List.empty

            while loop do
                match! parser (buff, size) with
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
let parseBuffer parseBytes : Parser<'a, 's> =
    fun (buff, c) ->
        let parsedPending =
            let bytes = buff.Pending.ToArray()
            if bytes.Length > c then Some(parseBytes bytes[c..]) else None

        match parsedPending with
        | Some v -> Task.FromResult v
        | None ->
            task {
                let! pending = buff.Pick()
                return pending.ToArray()[c..] |> parseBytes
            }
