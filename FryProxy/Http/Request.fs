module FryProxy.Http.Request

open System
open System.IO
open System.Text.RegularExpressions

open Headers
open FryProxy.Extension.Tuple

let private startLineRegex =
    Regex(@"(?<method>\w+)\s(?<uri>.+)\sHTTP/(?<version>\d\.\d)", RegexOptions.Compiled)

let createHttpRequestLine method uri version = { uri = uri; method = method; version = version }

let tryParseUri str =
    match Uri.TryCreate(str, UriKind.RelativeOrAbsolute) with
    | true, uri -> Some uri
    | false, _ -> None

let tryParseHttpMethod method =
    match Enum.TryParse<HttpMethodType> method with
    | true, value -> Some value
    | false, _ -> None

let tryParseHttpVersion ver =
    match Version.TryParse ver with
    | true, version -> Some version
    | false, _ -> None

let tryParseHttpRequestLine line =
    let m = startLineRegex.Match line

    if m.Success then
        Option.map3
            createHttpRequestLine
            (tryParseHttpMethod m.Groups.["method"].Value)
            (tryParseUri m.Groups.["uri"].Value)
            (tryParseHttpVersion m.Groups.["version"].Value)
    else
        None

let tryParseHttpRequestHeaders (lines: string seq) =
    let head, tail = lines |> Seq.decompose

    Option.map2
        tuple2
        (head |> Option.bind tryParseHttpRequestLine)
        (tail
         |> Seq.map tryParseHttpHeader
         |> Option.traverse)

let readHttpRequestHeaders (reader: TextReader) =
    reader
    |> TextReader.toSeq
    |> Seq.takeWhile String.isNotBlank
