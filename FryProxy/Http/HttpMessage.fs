module FryProxy.Http.HttpMessage

open System
open System.IO
open System.Text.RegularExpressions

let private startLineRegex =
    Regex(@"(?<method>\w+)\s(?<uri>.+)\sHTTP/(?<version>\d\.\d)", RegexOptions.Compiled)

let makeStartLine method uri version =
    { uri = uri; method = method; version = version }

let makeHttpHeader name values = { name = name; values = values }

let makeMessageHeader startLine headers =
    { startLine = startLine; headers = headers }

let tryParseUri uriString =
    match Uri.TryCreate(uriString, UriKind.RelativeOrAbsolute) with
    | true, uri -> Some uri
    | false, _ -> None

let tryParseHttpMethod method =
    match Enum.TryParse<HttpMethodType> method with
    | true, value -> Some value
    | false, _ -> None

let tryParseVersion ver =
    match Version.TryParse ver with
    | true, version -> Some version
    | false, _ -> None

let tryParseStartLine line =
    let m = startLineRegex.Match line

    if m.Success then
        Option.map3
            makeStartLine
            (tryParseHttpMethod m.Groups.["method"].Value)
            (tryParseUri m.Groups.["uri"].Value)
            (tryParseVersion m.Groups.["version"].Value)
    else
        None

let parseHeaderValue (value: string) =
    value.Split [| ',' |]
    |> Array.map (fun s -> s.Trim())
    |> Array.filter String.isNotBlank
    |> Array.toList

let tryParseHeaderLine (line: string) =
    match line.Split([| ':' |], 2) with
    | [| name; value |] when String.isNotBlank value ->
        Some { name = name; values = parseHeaderValue value }
    | _ -> None

let tryParseMessageHeader lines =
    let head = Seq.tryHead lines
    let tail = Seq.tail lines
    
    Option.map2
        makeMessageHeader
        (head |> Option.bind tryParseStartLine)
        (tail
         |> Seq.map tryParseHeaderLine
         |> Option.traverse)

let tryReadMessageHeader (reader: TextReader) =
    Seq.initInfinite (fun _ -> reader.ReadLine())
    |> Seq.takeWhile String.isNotBlank
    |> tryParseMessageHeader
