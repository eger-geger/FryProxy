namespace FryProxy.Http

open System
open System.Net.Http
open System.Text.RegularExpressions

[<Struct>]
type HttpRequestLine = { method: HttpMethod; uri: Uri; version: Version }

module RequestLine =

    let private regex =
        Regex(@"(?<method>\w+)\s+(?<uri>.+)\s+HTTP/(?<version>\d\.\d)", RegexOptions.Compiled)
    
    /// <summary>
    /// Curried factory for HttpRequestLine.
    /// </summary>
    let create method uri version =
        { uri = if isNull uri then nullArg (nameof uri) else uri
          method = method
          version = if isNull version then nullArg (nameof version) else version }

    let private fromMatch (m: Match) =
        let httpMethod = HttpMethod.Parse m.Groups["method"].Value

        Option.map2 (create httpMethod)
        <| Uri.tryParse m.Groups.["uri"].Value
        <| (m.Groups.["version"].Value |> Version.TryParse |> Option.ofAttempt)

    /// <summary>
    /// Attempt to parse HTTP Request line or return None.
    /// </summary>
    let tryParse = Regex.tryMatch regex >> Option.bind fromMatch
    
    /// <summary>
    /// Combine first HTTP request line components to string.
    /// </summary>
    let toString (line: HttpRequestLine) = $"{line.method} {line.uri.OriginalString} HTTP/{line.version}"
