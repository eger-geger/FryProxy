namespace FryProxy.IO.BufferedParser

open System.IO
open System.Threading.Tasks
open FryProxy.IO

exception ParseError of string

[<Struct>]
type ParseState = ParseState of Offset: uint16

type ParseState with

    static member val Zero = ParseState(0us)
    static member (+)(ParseState(offset), n: uint16) = ParseState(offset + n)
    static member (+)(ParseState(offset), n: int) = ParseState(offset + uint16 n)


type 'a ParseResult = (ParseState * 'a) ValueTask

type ('a, 's) Parser when 's :> Stream = 's ReadBuffer * ParseState -> 'a ParseResult
