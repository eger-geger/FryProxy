module FryProxy.Tests.Http2.Fields.AuthorityTests

#nowarn "3535" "3536"

open FryProxy.Http.Fields
open FryProxy.Http2.Fields
open FsUnitTyped
open NUnit.Framework

let decodeCases =
    [ TestCaseData("example.com").Returns(Some { Authority = "example.com" })
      TestCaseData("localhost:8080").Returns(Some { Authority = "localhost:8080" })
      TestCaseData("").Returns(None) ]

[<TestCaseSource(nameof decodeCases)>]
let testDecode value = TryDecode<Authority> value

let encodeCases =
    [ TestCaseData({ Authority = "example.com" }).Returns("example.com")
      TestCaseData({ Authority = "localhost:8080" }).Returns("localhost:8080") ]

[<TestCaseSource(nameof encodeCases)>]
let testEncode (authority: Authority) =
    (authority :> IFieldModel<Authority>).Encode()

[<Test>]
let testMetadata () =
    let fld = FieldOf { Authority = "example.com" }

    fld |> shouldEqual { Name = ":authority"; Value = "example.com" }
    Authority.TryFind [ fld ] |> shouldEqual (Some { Authority = "example.com" })
