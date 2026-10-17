module FryProxy.Tests.Http2.Fields.PathTests

#nowarn "3535" "3536"

open FryProxy.Http.Fields
open FryProxy.Http2.Fields
open FsUnitTyped
open NUnit.Framework

let decodeCases =
    [ TestCaseData("/").Returns(Some { Path = "/" })
      TestCaseData("/index.html").Returns(Some { Path = "/index.html" })
      TestCaseData("/api/v1/resource?id=123").Returns(Some { Path = "/api/v1/resource?id=123" })
      TestCaseData("*").Returns(Some { Path = "*" })
      TestCaseData("").Returns(None) ]

[<TestCaseSource(nameof decodeCases)>]
let testDecode value = TryDecode<Path> value

let encodeCases =
    [ TestCaseData({ Path = "/" }).Returns("/")
      TestCaseData({ Path = "/index.html" }).Returns("/index.html")
      TestCaseData({ Path = "/api/v1/resource?id=123" }).Returns("/api/v1/resource?id=123")
      TestCaseData({ Path = "*" }).Returns("*") ]

[<TestCaseSource(nameof encodeCases)>]
let testEncode (path: Path) =
    (path :> IFieldModel<Path>).Encode()

[<Test>]
let testMetadata () =
    let fld = FieldOf { Path = "/index.html" }

    fld |> shouldEqual { Name = ":path"; Value = "/index.html" }
    Path.TryFind [ fld ] |> shouldEqual (Some { Path = "/index.html" })
