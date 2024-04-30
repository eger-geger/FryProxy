module FryProxy.Http.Request

open System
open System.IO
open FryProxy.IO.BufferedParser

type RequestHeader = RequestLine * HttpHeader list


/// Parse first request line and headers from buffered input stream.
let requestHeaderParser: RequestHeader Parser =
    bufferedParser {
        let! requestLine = utf8LineParser |> Parser.flatmap RequestLine.tryParse |> Parser.commit

        let! headers =
            utf8LineParser
            |> Parser.flatmap HttpHeader.tryDecode
            |> Parser.commit
            |> Parser.eager

        let! separator = utf8LineParser |> Parser.commit

        if String.IsNullOrWhiteSpace separator then
            return requestLine, headers
    }


/// Attempt to split authority into host and port, using the given default port if one is omitted.
/// Returns None for malformed authority.
let trySplitHostPort (defaultPort: int) (authority: string) =
    match authority.Split(':') with
    | [| host |] -> Some(host, defaultPort)
    | [| host; port |] -> Int32.TryParse(port) |> Option.ofAttempt |> Option.map (fun port -> host, port)
    | _ -> None

/// Resolve requested resource identifier based on information from first line and headers.
let tryResolveResource (defaultPort: int) (line: RequestLine, headers: HttpHeader list) : Resource option =
    if line.uri.IsAbsoluteUri then
        { Host = line.uri.Host
          Port = line.uri.Port
          AbsoluteRef = line.uri.PathAndQuery }
        |> Some
    else
        headers
        |> HttpHeader.tryFind Headers.Host
        |> Option.bind HttpHeader.trySingleValue
        |> Option.bind (trySplitHostPort defaultPort)
        |> Option.map (fun (host, port) -> { Host = host; Port = port; AbsoluteRef = line.uri.OriginalString })
