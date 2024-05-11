module FryProxy.IO.BufferedParser.Parser

open System
open System.Threading.Tasks
open FryProxy.IO
open Microsoft.FSharp.Core


/// Parser evaluating to a constant value.
let unit a : Parser<'a, 's> =
    fun (_, u) -> Task.FromResult(Some(u, a))

/// Failed parser.
let failed: Parser<'a, 's> = fun _ -> Task.FromResult None

/// Discard bytes consumed by parser when it succeeds.
let commit (parser: Parser<'a, 's>) : Parser<'a, 's> =
    fun (buff, u) ->
        task {
            match! parser (buff, u) with
            | Some(0, a) -> return Some(0, a)
            | Some(u, a) ->
                buff.Discard u
                return Some(0, a)
            | None -> return None
        }

/// Commit and execute parser, returning parsed value.
let run (parser: Parser<'a, 's>) buff : 'a option Task =
    task {
        let! opt = commit parser (buff, 0)

        return opt |> Option.map snd
    }

/// Apply parser until condition evaluates to true or parser fails.
/// Returns the results of the last applied parser.
let takeWhile (cond: unit -> bool) (parser: Parser<unit, 's>) : Parser<unit, 's> =
    fun (buff, u) ->
        task {
            let mutable result = Some(u, ())

            while result.IsSome && cond () do
                let! pres = parser (buff, fst result.Value)
                result <- pres

            return result
        }

/// Transform value inside parser.
let map fn (parser: Parser<'a, 's>) : Parser<'b, 's> =
    fun state ->
        task {
            let! opt = parser state

            return opt |> Option.map (fun (a, b) -> a, fn b)
        }

/// Ignore parsed value.
let ignore p = map ignore p

/// Unwrap parsed value option, failing parser when empty.
let flatmap fn (parser: Parser<'a, 's>) : Parser<'b, 's> =
    fun state ->
        task {
            match! map fn parser state with
            | Some(u, opt) -> return Option.map (fun b -> u, b) opt
            | None -> return None
        }

/// Execute parsers sequentially
let bind (binder: 'a -> Parser<'b, 's>) (parser: Parser<'a, 's>) : Parser<'b, 's> =
    fun (buff, u) ->
        task {
            match! parser (buff, u) with
            | Some(c, a) -> return! binder a (buff, c)
            | None -> return None
        }

/// Fail the parser unless parsed value satisfies given condition.
let must cond =
    bind (fun a -> if cond a then unit a else failed)

/// Convert a function asynchronously reading from the buffer to parser.
let liftReader (fn: ReadBuffer<'s> -> 'a Task) : Parser<'a, 's> =
    fun (buff, _) ->
        task {
            let! result = fn buff
            return Some(0, result)
        }

/// Commit sub-parser repeatedly as long as it succeeds and return results as list.
let eager (parser: Parser<'a, 's>) : Parser<'a list, 's> =
    fun (buff, u) ->
        task {
            let mutable u = u
            let mutable loop = true
            let mutable results = List.empty

            while loop do
                match! parser (buff, u) with
                | Some(u', a) ->
                    u <- u'
                    results <- a :: results
                | None -> loop <- false

            return Some(u, List.rev results)
        }


/// <summary>
/// Create a parser consuming buffered bytes on each successful read.
/// </summary>
/// <param name="parseBytes">
/// Byte parsing function which is given reader buffer and returns
/// an optional tuple of consumed bytes count and parsed value.
/// </param>
let parseBuffer parseBytes : Parser<'a, 's> =
    fun (buff, u) ->
        let parseMem (mem: ReadOnlyMemory<byte>) =
            if mem.Length > u then
                mem.Slice(u).ToArray() |> parseBytes |> Option.map (fun (s, v) -> s + u, v)
            else
                None

        match parseMem buff.Pending with
        | None ->
            task {
                let! pending = buff.Pick()
                return parseMem pending
            }
        | some -> Task.FromResult some
