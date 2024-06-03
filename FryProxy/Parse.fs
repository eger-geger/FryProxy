namespace FryProxy

open System
open FryProxy.Http
open FryProxy.IO
open FryProxy.IO.BufferedParser
open Microsoft.FSharp.Core


/// Collection of parsers
type Parse =

    static let decodeLine decode =
        Parse.utf8Line |> Parser.flatmap decode |> Parser.commit

    static let parseHeader lineParser =
        bufferedParser {
            let! line = lineParser
            let! fields = Parse.fields
            return Header(line, fields)
        }

    static let parseMessage headerParser : Parser<Message<_>> =
        bufferedParser {
            let! Header(_, fields) as header = headerParser

            do! Parse.emptyLine

            let! body =
                match fields with
                | Message.HasContentLength n when n > 0UL -> Parser.bytes n |> Parser.map Sized
                | Message.HasChunkedEncoding -> Parse.chunkedBody
                | _ -> Parser.unit Empty

            return Message(header, body)
        }

    /// Parser of UTF8 encoded line terminated with a line break (included).
    static member val utf8Line: Parser<string> = Parser.decoder ByteBuffer.tryTakeUTF8Line

    /// Parse a single HTTP field.
    static member val field: Parser<Field> =
        bufferedParser {
            let! name, value = decodeLine Field.trySplitNameValue

            let! folds =
                Parse.utf8Line
                |> Parser.flatmap Field.tryFoldedLine
                |> Parser.eager
                |> Parser.commit

            return Field.decodeValues(String.Join(Tokens.WS, value :: folds)) |> Field.create name
        }

    /// Parse sequence of HTTP fields.
    static member val fields: Parser<Field list> = Parse.field |> Parser.eager

    /// Parse HTTP request first line.
    static member val requestLine: Parser<RequestLine> = decodeLine RequestLine.tryDecode

    /// Parse HTTP response first line.
    static member val statusLine: Parser<StatusLine> = decodeLine StatusLine.tryDecode

    /// Consume and ignore empty line.
    static member val emptyLine: Parser<unit> =
        Parse.utf8Line
        |> Parser.must "empty" String.IsNullOrWhiteSpace
        |> Parser.commit
        |> Parser.ignore

    /// Parse request line and HTTP headers.
    static member val requestHeader: Parser<RequestHeader> = parseHeader Parse.requestLine

    /// Parse response line and HTTP headers.
    static member val responseHeader: Parser<ResponseHeader> = parseHeader Parse.statusLine

    /// Parse chunk size and list of extensions preceding its content
    static member val chunkHeader: Parser<ChunkHeader> = decodeLine ChunkHeader.tryDecode

    /// Parse single HTTP chunk.
    static member chunk: Parser<Chunk> =
        bufferedParser {
            let! ChunkHeader(size, _) as header = Parse.chunkHeader

            let! body =
                if size = 0UL then
                    Parse.fields |> Parser.map Trailer
                else
                    Parser.bytes size |> Parser.map Content

            return Chunk(header, body)
        }

    /// Parse chunked content.
    static member chunkedBody: Parser<MessageBody> =
        let someChunk = Parse.chunk |> Parser.map ValueSome

        let tryChunk prev =
            match prev with
            | ValueNone -> someChunk
            | ValueSome(Chunk(ChunkHeader(size, _), _)) ->
                bufferedParser {
                    do! Parse.emptyLine

                    if size = 0UL then
                        return ValueNone
                    else
                        return! someChunk
                }

        Parser.unfold tryChunk |> Parser.map Chunked

    /// Parse HTTP request message.
    static member request = parseMessage Parse.requestHeader

    /// Parse HTTP response message.
    static member response = parseMessage Parse.responseHeader
