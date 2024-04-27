module FryProxy.Http.Request

open System
open FryProxy.IO.BufferedParser

type RequestHeader = HttpRequestLine * HttpHeader list

/// Parse first request line and headers from buffered input stream.
let parseRequestHeader: RequestHeader Parser =
    bufferedParser {
        let! requestLine = Parser.parseUTF8Line |> Parser.flatmap RequestLine.tryParse |> Parser.commit

        let! headers =
            Parser.parseUTF8Line
            |> Parser.flatmap Header.tryParse
            |> Parser.commit
            |> Parser.eager

        let! separator = Parser.parseUTF8Line |> Parser.commit

        if String.IsNullOrWhiteSpace separator then
            return requestLine, headers
    }

let parseHostAndPort (host: string) =
    match host.LastIndexOf(':') with
    | -1 -> host, None
    | ix ->
        Int32.TryParse(host.Substring(ix + 1))
        |> Option.ofAttempt
        |> Tuple.create2 (host.Substring(0, ix))

let tryResolveDestination (line: HttpRequestLine, headers: HttpHeader list) =
    if line.uri.IsAbsoluteUri then
        Some(line.uri.Host, Some(line.uri.Port), line.uri.PathAndQuery)
    else
        headers
        |> List.tryFind (Header.hasName Header.Names.Host)
        |> Option.bind Header.tryLast
        |> Option.map parseHostAndPort
        |> Option.map (Tuple.append2 line.uri.OriginalString)
