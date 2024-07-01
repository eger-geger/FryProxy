[<RequireQualifiedAccess>]
module FryProxy.Http.Parse

open System
open FryProxy.IO
open FryProxy.Extension
open FryProxy.IO.BufferedParser
open Microsoft.FSharp.Core

/// Parser of UTF8 encoded line terminated with a line break (included).
let utf8Line = Parser.decoder ByteBuffer.tryTakeUTF8Line

let decodeLine decode =
    utf8Line |> Parser.flatmap decode |> Parser.commit

/// Consume and ignore empty line.
let emptyLine: Parser<unit> =
    utf8Line
    |> Parser.must "empty" String.IsNullOrWhiteSpace
    |> Parser.commit
    |> Parser.ignore

/// Parse a single HTTP field.
let field: Parser<Field> =
    bufferedParser {
        let! name, value = decodeLine Field.trySplitNameValue

        let! folds = utf8Line |> Parser.flatmap Field.tryFoldedLine |> Parser.eager |> Parser.commit

        return Field.decodeValues(String.Join(Tokens.WS, value :: folds)) |> Field.create name
    }

/// Parse sequence of HTTP fields.
let fields: Parser<Field list> = field |> Parser.eager

/// Parse HTTP request first line.
let requestLine: Parser<RequestLine> =
    decodeLine(RequestLine.tryDecode >> voption.toOption)

/// Parse HTTP response first line.
let statusLine: Parser<StatusLine> =
    decodeLine(StatusLine.tryDecode >> voption.toOption)

let inline parseHeader lineParser =
    bufferedParser {
        let! line = lineParser
        let! fields = fields
        return Header(line, fields)
    }

/// Parse request line and HTTP headers.
let requestHeader: Parser<RequestHeader> = parseHeader requestLine

/// Parse response line and HTTP headers.
let responseHeader: Parser<ResponseHeader> = parseHeader statusLine

/// Parse chunk size and list of extensions preceding its content
let chunkHeader: Parser<ChunkHeader> = decodeLine ChunkHeader.tryDecode

/// Parse single HTTP chunk.
let chunk: Parser<Chunk> =
    bufferedParser {
        let! ChunkHeader(size, _) as header = chunkHeader

        let! body =
            if size = 0UL then
                fields |> Parser.map Trailer
            else
                Parser.bytes size |> Parser.map Content

        return Chunk(header, body)
    }

/// Parse chunked content.
let chunkedBody: Parser<MessageBody> =
    let someChunk = chunk |> Parser.map ValueSome

    let tryChunk prev =
        match prev with
        | ValueNone -> someChunk
        | ValueSome(Chunk(ChunkHeader(size, _), _)) ->
            bufferedParser {
                do! emptyLine

                if size = 0UL then
                    return ValueNone
                else
                    return! someChunk
            }

    Parser.unfold tryChunk |> Parser.map Chunked

let inline parseMessage headerParser : Parser<Message<_>> =
    bufferedParser {
        let! Header(_, fields) as header = headerParser

        do! emptyLine

        let! body =
            match fields with
            | Message.HasContentLength n when n > 0UL -> Parser.bytes n |> Parser.map Sized
            | Message.HasChunkedEncoding -> chunkedBody
            | _ -> Parser.unit Empty

        return Message(header, body)
    }

/// Parse HTTP request message.
let request: Parser<RequestMessage> = parseMessage requestHeader

/// Parse HTTP response message.
let response: Parser<ResponseMessage> = parseMessage responseHeader
