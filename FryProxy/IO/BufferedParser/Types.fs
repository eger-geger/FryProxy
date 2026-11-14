namespace FryProxy.IO.BufferedParser

open System
open System.Threading.Tasks
open FryProxy.IO

type ParseError(message: string) =
    inherit Exception(message)

[<AutoOpen>]
module ParseError =
    let (|ParseError|_|) (ex: exn) =
        match ex with
        | :? ParseError as pe -> ValueSome pe.Message
        | _ -> ValueNone

[<Struct>]
type ActiveState = { Offset: uint16 }

[<Struct>]
type ParseState =
    | Running of Active: ActiveState
    | Yielded of Paused: IConsumable

type 'a ParseResult = (ParseState * 'a) ValueTask
type 'a Parser = ReadBuffer * ParseState -> 'a ParseResult
type 'a StrictParser = ReadBuffer * ActiveState -> 'a ParseResult

module ParseResult =

    let unit = ValueTask.FromResult<ParseState * 'a>

    let inline liftTask (task: 'a Task) = ValueTask<'a>(task)

    let inline error msg =
        ValueTask.FromException<ParseState * 'a>(ParseError msg)

    let inline bind (binder: 'a -> 'b ValueTask) (valueTask: 'a ValueTask) =
        liftTask
        <| task {
            let! res = valueTask
            return! binder res
        }

    let inline map fn = fn >> ValueTask.FromResult |> bind
