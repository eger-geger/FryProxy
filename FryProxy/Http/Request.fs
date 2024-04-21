module FryProxy.Http.Request

open System
open FryProxy.IO.BufferedParser


/// <summary>
/// Parse first request line and headers from a buffered input stream.
/// </summary>
let parseRequest: (HttpRequestLine * HttpHeader list) BufferedParser =
    let parseRequestLine = parseUTF8Line |> map RequestLine.tryParse |> flatOpt

    let parseHeaders =
        parseUTF8Line
        |> map Header.tryParse
        |> flatOpt
        |> eager
        |> orElse (unit List.Empty)

    join Tuple.create2 parseRequestLine parseHeaders


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
