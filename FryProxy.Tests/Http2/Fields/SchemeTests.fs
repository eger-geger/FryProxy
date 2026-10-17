module FryProxy.Tests.Http2.Fields.SchemeTests

#nowarn "3535" "3536"

open FryProxy.Http.Fields
open FryProxy.Http2.Fields
open FsUnitTyped
open NUnit.Framework

let decodeCases =
    [ TestCaseData("http").Returns(Some Scheme.Http)
      TestCaseData("https").Returns(Some Scheme.Https)
      TestCaseData("ws").Returns(Some { Scheme = "ws" })
      TestCaseData("wss").Returns(Some { Scheme = "wss" })
      TestCaseData("").Returns(None) ]

[<TestCaseSource(nameof decodeCases)>]
let testDecode value = TryDecode<Scheme> value

let encodeCases =
    [ TestCaseData(Scheme.Http).Returns("http")
      TestCaseData(Scheme.Https).Returns("https")
      TestCaseData({ Scheme = "ws" }).Returns("ws") ]

[<TestCaseSource(nameof encodeCases)>]
let testEncode (scheme: Scheme) =
    (scheme :> IFieldModel<Scheme>).Encode()

[<Test>]
let testMetadata () =
    let fld = FieldOf Scheme.Https

    fld |> shouldEqual { Name = ":scheme"; Value = "https" }
    Scheme.TryFind [ fld ] |> shouldEqual (Some Scheme.Https)
