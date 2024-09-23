module FryProxy.Tests.Http.StatusLineProps

open System
open FsCheck.FSharp
open FsCheck.NUnit

open FryProxy.Http

let statusCodes =
    [ 100us; 101us ]
    @ [ 200us .. 206us ]
    @ [ 300us .. 305us ]
    @ [ 307us ]
    @ [ 400us .. 417us ]
    @ [ 426us ]
    @ [ 500us .. 505us ]

let statusCodeGen = Gen.elements statusCodes

let httpVersionGen = Gen.elements [ Version(1, 1); Version(1, 0) ]

let statusLineGen =
    gen {
        let! version = httpVersionGen
        let! statusCode = statusCodeGen
        let! reasonOpt = Gen.constant statusCode |> Gen.map ReasonPhrase.forStatusCode |> Gen.optionOf
        let reason = Option.defaultValue String.Empty reasonOpt

        return version, statusCode, reason
    }

type Generators =
    static member StatusLines() = Arb.fromGen statusLineGen

[<Property(MaxTest = 100, Arbitrary = [| typeof<Generators> |])>]
let canEncodeAndDecode (version, code, reason) =
    let encoded = $"HTTP/{version} {code} {reason}"
    let decoded = { Version = version; Code = code; Reason = reason }

    let canDecodeWithSuffix suffix label =
        StatusLine.tryDecode (encoded + suffix) = ValueSome decoded |> Prop.label label

    StartLine.encode decoded = encoded |> Prop.label "Encoding"
    .&. canDecodeWithSuffix "" "Decode plain"
    .&. canDecodeWithSuffix "\r" @"Decode ending with \r"
    .&. canDecodeWithSuffix "\n" @"Decode ending with \n"
    .&. canDecodeWithSuffix "\r\n" @"Decode ending with \r\n"
    |> Prop.classify (reason = "") "Empty reason"

[<Property(MaxTest = 10, Arbitrary = [| typeof<Generators> |])>]
let canCreateDefault () =
    let statusLinesWithNonEmptyReason =
        Generators.StatusLines() |> Arb.filter (fun (_, _, reason) -> reason <> "")

    let canCreateStatusLine (_, code, reason) =
        StatusLine.createDefault code = { Version = Version(1, 1); Code = code; Reason = reason }
        |> Prop.label $"Create status line from {code}"

    canCreateStatusLine |> Prop.forAll statusLinesWithNonEmptyReason


[<Property(MaxTest = 10, Arbitrary = [| typeof<Generators> |])>]
let cannotDecodeMalformed (version: Version, code: uint16, reason: string) =
    let prop line =
        StatusLine.tryDecode line = ValueNone |> Prop.label $"Fails to decode '{line}'"

    prop $"HTTP {code} {reason}"
    .&. prop $"HTTP/{version} {reason}"
    .&. prop $"HTTP/{version} AA {reason}"
