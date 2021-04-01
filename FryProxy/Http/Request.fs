module FryProxy.Http.Request

open System
open System.IO
open FryProxy

let tryParseHeaders (lines: string seq) =
    let head, tail = lines |> Seq.decompose

    Option.map2
        Tuple.create2
        (head |> Option.bind RequestLine.tryParse)
        (tail |> Seq.map Header.tryParse |> Option.traverse)

let readHeaders stream =
    use reader = new PlainStreamReader(stream)

    reader
    |> TextReader.readLines
    |> Seq.takeWhile String.isNotBlank


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
