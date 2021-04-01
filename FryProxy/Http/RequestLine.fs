namespace FryProxy.Http

open System
open System.Text.RegularExpressions
open FryProxy.Http

type HttpRequestLine = { method: HttpMethodType; uri: Uri; version: Version }

module RequestLine =

    let private regex =
        Regex(@"(?<method>\w+)\s+(?<uri>.+)\s+HTTP/(?<ver>\d\.\d)", RegexOptions.Compiled)

    let create method uri version =
        { uri = if isNull uri then nullArg (nameof uri) else uri
          method = method
          version = if isNull version then nullArg (nameof version) else version }

    let private fromMatch (m: Match) =
        let methodOpt = MethodType.tryParse m.Groups.["method"].Value
        let uriOpt = m.Groups.["uri"].Value |> Uri.tryParse

        let verOpt =
            m.Groups.["ver"].Value
            |> Version.TryParse
            |> Option.ofAttempt

        Option.map3 create methodOpt uriOpt verOpt

    let tryParse line =
        line
        |> Regex.tryMatch regex
        |> Option.bind fromMatch

    let toString (line: HttpRequestLine) = $"{line.method} {line.uri.OriginalString} HTTP/{line.version}"
