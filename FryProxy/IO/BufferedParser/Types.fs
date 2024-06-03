namespace FryProxy.IO.BufferedParser

open System.Threading.Tasks
open FryProxy.IO

exception ParseError of string

[<Struct>]
type ParseMode =
    | StrictMode
    | LazyMode of Pending: IConsumable

[<Struct>]
type ParseState = { Offset: uint16; Mode: ParseMode }

type ParseState with

    static member val Zero = { Offset = 0us; Mode = StrictMode }

    static member (+)(state: ParseState, n: uint16) =
        { state with Offset = n + state.Offset }

    static member (+)(state: ParseState, n: int) =
        { state with Offset = state.Offset + uint16 n }


type 'a ParseResult = (ParseState * 'a) ValueTask

type 'a Parser = ReadBuffer * ParseState -> 'a ParseResult
