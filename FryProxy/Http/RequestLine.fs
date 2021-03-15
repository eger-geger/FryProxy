namespace FryProxy.Http

open System
open System.Text.RegularExpressions

type HttpRequestLine = { method: HttpMethodType; uri: Uri; version: Version }

module RequestLine =
    
    let private requestLineRegex =
        Regex(@"(?<method>\w+)\s(?<uri>.+)\sHTTP/(?<version>\d\.\d)", RegexOptions.Compiled)
    
    let create method uri version = { uri = uri; method = method; version = version }
    
    let private tryParseUri str =
        match Uri.TryCreate(str, UriKind.RelativeOrAbsolute) with
        | true, uri -> Some uri
        | false, _ -> None

    let private tryParseHttpVersion ver =
        match Version.TryParse ver with
        | true, version -> Some version
        | false, _ -> None

    let tryParse line =
        let m = requestLineRegex.Match line

        if m.Success then
            Option.map3
                create
                (MethodType.tryParse m.Groups.["method"].Value)
                (tryParseUri m.Groups.["uri"].Value)
                (tryParseHttpVersion m.Groups.["version"].Value)
        else
            None

