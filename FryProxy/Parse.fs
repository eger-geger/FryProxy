namespace FryProxy

open System
open FryProxy.Http
open FryProxy.IO
open FryProxy.IO.BufferedParser


/// Collection of parsers
type Parse<'s> when 's :> System.IO.Stream =

    /// Parser of UTF8 encoded line terminated with a line break (included).
    static member val utf8Line: Parser<string, 's> = Parser.parseBuffer ByteBuffer.tryTakeUTF8Line

    /// Parse HTTP message headers
    static member headers: Parser<Field list, 's> =
        Parse.utf8Line
        |> Parser.flatmap Field.tryDecode
        |> Parser.commit
        |> Parser.eager

    /// Parse HTTP request first line
    static member requestLine: Parser<RequestLine, 's> =
        Parse.utf8Line |> Parser.flatmap RequestLine.tryDecode |> Parser.commit

    /// Consume and ignore empty line.
    static member emptyLine: Parser<unit, 's> =
        Parse.utf8Line
        |> Parser.must String.IsNullOrWhiteSpace
        |> Parser.commit
        |> Parser.ignore

    /// Parse request line and HTTP headers followed by line break
    static member requestHeader: Parser<Request.RequestHeader, 's> =
        bufferedParser {
            let! requestLine = Parse.requestLine
            let! headers = Parse.headers
            return requestLine, headers
        }

    /// Parse chunk size and list of extensions preceding its content
    static member val chunkHeader: Parser<ChunkHeader, 's> =
        Parse.utf8Line |> Parser.flatmap ChunkHeader.tryDecode |> Parser.commit

    /// Parse sequence of HTTP chunks letting a function read chunk body.
    static member chunks readChunk : Parser<unit, 's> =
        bufferedParser {
            let mutable lastChunk = false

            while not lastChunk do
                let! header = Parse.chunkHeader
                lastChunk <- header.Size = 0UL
                do! readChunk header |> Parser.liftReader
                do! Parse.emptyLine
        }

    /// Parse HTTP request letting a function process the body.
    static member request readBody : Parser<unit, 's> =
        bufferedParser {
            let! line, fields = Parse.requestHeader
            do! Parse.emptyLine
            do! (line, fields) |> readBody (Message.inferBodyType fields) |> Parser.liftReader
        }
