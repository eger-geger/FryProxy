namespace FryProxy.Http

open System
open System.Net.Http
open System.Text.RegularExpressions
open FryProxy.Http

[<Struct>]
type HttpRequestLine =
    { method: HttpMethod
      uri: Uri
      version: Version }

module RequestLine =

    let private regex =
        Regex(@"(?<method>\w+)\s+(?<uri>.+)\s+HTTP/(?<ver>\d\.\d)", RegexOptions.Compiled)

    let create method uri version =
        { uri = if isNull uri then nullArg (nameof uri) else uri
          method = method
          version = if isNull version then nullArg (nameof version) else version }

    let private fromMatch (m: Match) =
        let httpMethod = HttpMethod.Parse m.Groups.["method"].Value

        Option.map2 (create httpMethod)
        <| Uri.tryParse m.Groups.["uri"].Value
        <| (m.Groups.["ver"].Value |> Version.TryParse |> Option.ofAttempt)

    let tryParse line =
        line |> Regex.tryMatch regex |> Option.bind fromMatch

    let toString (line: HttpRequestLine) =
        $"{line.method} {line.uri.OriginalString} HTTP/{line.version}"
