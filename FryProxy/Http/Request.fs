module FryProxy.Http.Request

open System
open FryProxy.IO

module Parser = BufferedParser

/// <summary>
/// Parse first request line and headers from a buffered input stream.
/// </summary>
let parseRequest: (HttpRequestLine * HttpHeader list) BufferedParser.Parser =
    let parseRequestLine =
        Parser.parseUTF8Line |> Parser.map RequestLine.tryParse |> Parser.flatOpt

    let parseHeaders =
        Parser.parseUTF8Line
        |> Parser.map Header.tryParse
        |> Parser.flatOpt
        |> Parser.eager
        |> Parser.orElse (Parser.unit List.Empty)

    Parser.map2 Tuple.create2 parseRequestLine parseHeaders


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
