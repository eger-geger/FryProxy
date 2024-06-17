namespace FryProxy.Http

open System
open System.Net.Http
open System.Text.RegularExpressions

[<Struct>]
type RequestLine =
    { Method: HttpMethod
      Target: string
      Version: Version }

    interface StartLine with

        member this.Version = this.Version

        member this.Encode() =
            $"{this.Method} {this.Target} HTTP/{this.Version}"

[<RequireQualifiedAccess>]
module RequestLine =

    let private regex =
        Regex(@"(?<method>\w+)\s+(?<uri>.+)\s+HTTP/(?<version>\d\.\d)", RegexOptions.Compiled)

    /// Curried factory for HttpRequestLine.
    let create method uri version =
        { Target = if isNull uri then nullArg(nameof uri) else uri
          Method = method
          Version =
            if isNull version then
                nullArg(nameof version)
            else
                version }

    let private fromMatch (m: Match) =
        let httpMethod = HttpMethod.Parse m.Groups["method"].Value

        m.Groups["version"].Value
        |> Version.TryParse
        |> Option.ofAttempt
        |> Option.map(create httpMethod m.Groups["uri"].Value)

    /// Attempt to parse HTTP Request line or return None.
    let tryDecode = Regex.tryMatch regex >> Option.bind fromMatch
