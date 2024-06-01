namespace FryProxy

open System
open System.IO
open FryProxy.Http
open FryProxy.IO
open FryProxy.IO.BufferedParser
open Microsoft.FSharp.Core


/// Collection of parsers
type Parse<'s> when 's :> Stream =

    static let decodeLine decode =
        Parse.utf8Line |> Parser.flatmap decode |> Parser.commit

    static let parseHeader lineParser =
        bufferedParser {
            let! line = lineParser
            let! fields = Parse.fields
            return Header(line, fields)
        }

    static let parseMessage headerParser : Parser<Message<_>, 's> =
        bufferedParser {
            let! Header(_, fields) as header = headerParser

            do! Parse.emptyLine

            let! body =
                match fields with
                | Message.HasContentLength n when n > 0UL -> Parser.bytes n |> Parser.map (fun x -> Sized(n, x))
                | Message.HasChunkedEncoding -> Parse.chunkedBody
                | _ -> Parser.unit Empty

            return Message(header, body)
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
        |> Parser.must "empty" String.IsNullOrWhiteSpace
        |> Parser.commit
        |> Parser.ignore

    /// Parse request line and HTTP headers.
    static member val requestHeader: Parser<RequestHeader, 's> = parseHeader Parse.requestLine

    /// Parse response line and HTTP headers.
    static member val responseHeader: Parser<ResponseHeader, 's> = parseHeader Parse.statusLine

    /// Parse chunk size and list of extensions preceding its content
    static member val chunkHeader: Parser<ChunkHeader, 's> = decodeLine ChunkHeader.tryDecode

    /// Parse single HTTP chunk.
    static member chunk: Parser<Chunk, 's> =
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
    static member chunkedBody: Parser<MessageBody, 's> =
        let tryStep state =
            let chunkParser =
                Parse.chunk
                |> Parser.map (fun (Chunk(ChunkHeader(size, _), _) as chunk) -> Some(size), chunk)
            
            match state with
            | Some 0UL ->
                bufferedParser {
                    do! Parse.emptyLine
                    return! Parser.failed "No more chunks"
                }
            | None -> chunkParser
            | Some _ ->
                bufferedParser {
                    do! Parse.emptyLine
                    return! chunkParser
                }
                

        Parser.unfold tryStep None |> Parser.map Chunked

    /// Parse HTTP request message.
    static member request = parseMessage Parse.requestHeader

    /// Parse HTTP response message.
    static member response = parseMessage Parse.responseHeader
