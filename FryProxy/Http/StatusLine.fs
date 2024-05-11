namespace FryProxy.Http

open System
open System.Text.RegularExpressions

[<Struct>]
type StatusLine = { version: Version; code: uint16; reason: string }

[<RequireQualifiedAccess>]
module StatusLine =

    let private regex =
        Regex(@"HTTP/(?<ver>\d.\d)\s+(?<code>\d+)\s+(?<reason>.+)", RegexOptions.Compiled)

    let create version code reason =
        if isNull version then
            nullArg (nameof version)

        if String.IsNullOrEmpty reason then
            invalidArg (nameof reason) "Null or empty reason phrase"

        { version = version; code = code; reason = reason }

    let createDefault code =
        create (Version(1, 1)) code (ReasonPhrase.forStatusCode code)

    let private fromMatch (m: Match) =
        let verOpt = m.Groups.["ver"].Value |> Version.TryParse |> Option.ofAttempt

        let codeOpt = m.Groups.["code"].Value |> UInt16.TryParse |> Option.ofAttempt

        Option.map3 create verOpt codeOpt (Some m.Groups.["reason"].Value)

    let tryDecode line =
        Regex.tryMatch regex line |> Option.bind fromMatch

    let encode (line: StatusLine) =
        $"HTTP/{line.version} {line.code} {line.reason}"

type StatusLine with

    member line.Encode() = StatusLine.encode line
