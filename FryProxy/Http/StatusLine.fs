namespace FryProxy.Http

open System
open System.Text.RegularExpressions
open FryProxy.Extension

[<Struct>]
type StatusLine =
    { Version: Version
      Code: uint16
      Reason: string }

    interface StartLine with
        member this.Version = this.Version

        member this.Encode() =
            $"HTTP/{this.Version} {this.Code} {this.Reason}"

[<RequireQualifiedAccess>]
module StatusLine =

    let private regex =
        Regex(@"^HTTP/(?<ver>\d.\d)\s+(?<code>\d+)\s+(?<reason>(?:\S+\s*?)*)\s*$", RegexOptions.Compiled)

    let create version code reason =
        if isNull version then
            nullArg(nameof version)

        { Version = version; Code = code; Reason = reason }

    let createDefault code =
        create (Version(1, 1)) code (ReasonPhrase.forStatusCode code)

    let private fromMatch (m: Match) =
        ValueOption.map2 create
        <| (m.Groups.["ver"].Value |> Version.TryParse |> voption.ofAttempt)
        <| (m.Groups.["code"].Value |> UInt16.TryParse |> voption.ofAttempt)
        |> ValueOption.map((|>) m.Groups.["reason"].Value)

    let tryDecode = regex.tryMatch >> ValueOption.bind fromMatch
