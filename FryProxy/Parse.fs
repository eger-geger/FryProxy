namespace FryProxy

open System
open System.IO
open System.Threading.Tasks
open FryProxy.Http
open FryProxy.IO
open FryProxy.IO.BufferedParser


/// HTTP message content parser being called after parsing headers.
/// Receives the kind of content determined from headers, headers themselves and buffer for reading the content.
type ContentParser<'l, 's> when 'l :> StartLine and 's :> Stream =
    MessageBodyType -> 'l MessageHeader -> ReadBuffer<'s> -> Task<unit>

/// Collection of parsers
type Parse<'s> when 's :> Stream =

    static let decodeLine decode =
        Parse.utf8Line |> Parser.flatmap decode |> Parser.commit

    static let parseHeader firstLine =
        bufferedParser {
            let! line = firstLine
            let! fields = Parse.fields
            return Header(line, fields)
        }

    static let parseMessage header (readBody: ContentParser<_, _>) : Parser<unit, 's> =
        bufferedParser {
            let! Header(line, fields) = header

            do! Parse.emptyLine

            do!
                Header(line, fields)
                |> readBody (Message.inferBodyType fields)
                |> Parser.liftReader
        }

    /// Parser of UTF8 encoded line terminated with a line break (included).
    static member val utf8Line: Parser<string, 's> = Parser.parseBuffer ByteBuffer.tryTakeUTF8Line

    /// Parse a single HTTP field.
    static member val field: Parser<Field, 's> =
        bufferedParser {
            let! name, value = decodeLine Field.trySplitNameValue

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
    static member val requestLine: Parser<RequestLine, 's> = decodeLine RequestLine.tryDecode

    /// Parse HTTP response first line.
    static member val statusLine: Parser<StatusLine, 's> = decodeLine StatusLine.tryDecode

    /// Consume and ignore empty line.
    static member val emptyLine: Parser<unit, 's> =
        Parse.utf8Line
        |> Parser.must String.IsNullOrWhiteSpace
        |> Parser.commit
        |> Parser.ignore

    /// Parse request line and HTTP headers.
    static member val requestHeader: Parser<Request.Header, 's> = parseHeader Parse.requestLine

    /// Parse response line and HTTP headers.
    static member val responseHeader: Parser<Response.Header, 's> = parseHeader Parse.statusLine

    /// Parse chunk size and list of extensions preceding its content
    static member val chunkHeader: Parser<ChunkHeader, 's> = decodeLine ChunkHeader.tryDecode

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

    /// Parse HTTP request delegating content processing.
    static member request = parseMessage Parse.requestHeader

    /// Parse HTTP response delegating content processing.
    static member response = parseMessage Parse.responseHeader
