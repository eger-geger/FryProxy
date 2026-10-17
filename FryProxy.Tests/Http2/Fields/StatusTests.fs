module FryProxy.Tests.Http2.Fields.StatusTests

#nowarn "3535" "3536"

open FryProxy.Http.Fields
open FryProxy.Http2.Fields
open FsUnitTyped
open NUnit.Framework

let decodeCases =
    [ TestCaseData("200").Returns(Some { Status = 200us })
      TestCaseData("204").Returns(Some { Status = 204us })
      TestCaseData("304").Returns(Some { Status = 304us })
      TestCaseData("400").Returns(Some { Status = 400us })
      TestCaseData("404").Returns(Some { Status = 404us })
      TestCaseData("500").Returns(Some { Status = 500us })
      TestCaseData("").Returns(None)
      TestCaseData("abc").Returns(None)
      TestCaseData("-1").Returns(None)
      TestCaseData("65536").Returns(None) ]

[<TestCaseSource(nameof decodeCases)>]
let testDecode value = TryDecode<Status> value

let encodeCases =
    [ TestCaseData({ Status = 200us }).Returns("200")
      TestCaseData({ Status = 204us }).Returns("204")
      TestCaseData({ Status = 304us }).Returns("304")
      TestCaseData({ Status = 404us }).Returns("404")
      TestCaseData({ Status = 500us }).Returns("500") ]

[<TestCaseSource(nameof encodeCases)>]
let testEncode (status: Status) =
    (status :> IFieldModel<Status>).Encode()

[<Test>]
let testMetadata () =
    let fld = FieldOf { Status = 200us }

    fld |> shouldEqual { Name = ":status"; Value = "200" }
    Status.TryFind [ fld ] |> shouldEqual (Some { Status = 200us })
