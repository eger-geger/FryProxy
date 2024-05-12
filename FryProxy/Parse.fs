namespace FryProxy

open System
open FryProxy.Http
open FryProxy.IO
open FryProxy.IO.BufferedParser


/// Collection of parsers
type Parse<'s> when 's :> System.IO.Stream =

    /// Parser of UTF8 encoded line terminated with a line break (included).
    static member val utf8Line: Parser<string, 's> = Parser.parseBuffer ByteBuffer.tryTakeUTF8Line

    /// Parse a single HTTP field.
    static member val field: Parser<Field, 's> =
        bufferedParser {
            let! name, value = Parse.utf8Line |> Parser.flatmap Field.trySplitNameValue |> Parser.commit

            let! folds =
                Parse.utf8Line
                |> Parser.flatmap Field.tryFoldedLine
                |> Parser.eager
                |> Parser.commit

            return Field.decodeValues (String.Join(Tokens.WS, value :: folds)) |> Field.create name
        }


    /// Parse sequence of HTTP fields.
    static member val fields: Parser<Field list, 's> = Parse.field |> Parser.eager

    /// Parse HTTP request first line.
    static member val requestLine: Parser<RequestLine, 's> =
        Parse.utf8Line |> Parser.flatmap RequestLine.tryDecode |> Parser.commit

    /// Consume and ignore empty line.
    static member val emptyLine: Parser<unit, 's> =
        Parse.utf8Line
        |> Parser.must String.IsNullOrWhiteSpace
        |> Parser.commit
        |> Parser.ignore

    /// Parse request line and HTTP headers followed by line break
    static member val requestHeader: Parser<Request.RequestHeader, 's> =
        bufferedParser {
            let! requestLine = Parse.requestLine
            let! headers = Parse.fields
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

                let! trailer = if lastChunk then Parse.fields else Parser.unit List.empty
                do! readChunk (header, trailer) |> Parser.liftReader
                do! Parse.emptyLine
        }

    /// Parse HTTP request letting a function process the body.
    static member request readBody : Parser<unit, 's> =
        bufferedParser {
            let! line, fields = Parse.requestHeader
            do! Parse.emptyLine
            do! (line, fields) |> readBody (Message.inferBodyType fields) |> Parser.liftReader
        }
