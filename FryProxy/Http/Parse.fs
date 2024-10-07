[<RequireQualifiedAccess>]
module FryProxy.Http.Parse

open System
open FryProxy.IO
open FryProxy.Extension
open FryProxy.IO.BufferedParser
open Microsoft.FSharp.Core

/// Parser of UTF8 encoded line terminated with a line break (included).
let utf8Line = Parser.decoder ByteBuffer.tryTakeUTF8Line

/// Consume a decoded value. 
let decodeLine decode =
    utf8Line |> Parser.flatmap decode |> Parser.commit

/// Consume and ignore empty line.
let emptyLine: Parser<unit> =
    utf8Line
    |> Parser.must "empty" String.IsNullOrWhiteSpace
    |> Parser.commit
    |> Parser.ignore

/// Consume a single HTTP field.
let field: Parser<Field> =
    bufferedParser {
        let! name, value = decodeLine Field.trySplitNameValue

        let! folds = utf8Line |> Parser.flatmap Field.tryFoldedLine |> Parser.eager |> Parser.commit

        return Field.decodeValues(String.Join(Tokens.WS, value :: folds)) |> Field.create name
    }

/// Consume sequence of HTTP fields.
let fields: Parser<Field list> = field |> Parser.eager

/// Consume HTTP request first line.
let requestLine: Parser<RequestLine> =
    decodeLine(RequestLine.tryDecode >> voption.toOption)

/// Consume HTTP response first line.
let statusLine: Parser<StatusLine> =
    decodeLine(StatusLine.tryDecode >> voption.toOption)

/// Consume "Continue" status line.
let continueLine: Parser<StatusLine> =
    StatusLine.tryDecode
    >> ValueOption.bind(fun line ->
        if line.Code = 100us then
            ValueSome line
        else
            ValueNone)
    >> ValueOption.toOption
    |> decodeLine

let inline parseHeader lineParser =
    bufferedParser {
        let! line = lineParser
        let! fields = fields
        return { StartLine = line; Fields = fields }
    }

/// Consume request line and HTTP headers.
let requestHeader: RequestHeader Parser = parseHeader requestLine

/// Consume response line and HTTP headers.
let responseHeader: ResponseHeader Parser = parseHeader statusLine

/// Consume chunk size and list of extensions preceding its content.
let chunkHeader: ChunkHeader Parser = decodeLine ChunkHeader.tryDecode

/// Consume single HTTP chunk.
let chunk: Chunk Parser =
    bufferedParser {
        let! header = chunkHeader

        let! body =
            if header.Size = 0UL then
                fields |> Parser.map Trailer
            else
                Parser.bytes header.Size |> Parser.map Content

        return { Header = header; Body = body }
    }

/// Parse chunked content.
let chunkedBody: MessageBody Parser =
    let someChunk = chunk |> Parser.map ValueSome

    let tryChunk prev =
        match prev with
        | ValueNone -> someChunk
        | ValueSome({ Chunk.Header = { Size = size } }) ->
            bufferedParser {
                do! emptyLine

                if size = 0UL then
                    return ValueNone
                else
                    return! someChunk
            }

    Parser.unfold tryChunk |> Parser.map Chunked

let inline parseMessage headerParser : _ Message Parser =
    bufferedParser {
        let! header = headerParser

        do! emptyLine

        let! body =
            match header.Fields with
            | Message.HasContentLength n when n > 0UL -> Parser.bytes n |> Parser.map Sized
            | Message.HasChunkedEncoding -> chunkedBody
            | _ -> Parser.unit Empty

        return { Header = header; Body = body }
    }

/// Consume HTTP request message.
let request: RequestMessage Parser = parseMessage requestHeader

/// Consume HTTP response message.
let response: ResponseMessage Parser = parseMessage responseHeader
