module FryProxy.Tests.Http2.Fields.MethodTests

#nowarn "3535" "3536"

open System.Net.Http
open FryProxy.Http
open FryProxy.Http.Fields
open FryProxy.Http2.Fields
open FsUnitTyped
open NUnit.Framework

let decodeCases =
    [ TestCaseData("GET").Returns(Some { Method = HttpMethod.Get })
      TestCaseData("POST").Returns(Some { Method = HttpMethod.Post })
      TestCaseData("PUT").Returns(Some { Method = HttpMethod.Put })
      TestCaseData("DELETE").Returns(Some { Method = HttpMethod.Delete })
      TestCaseData("CONNECT").Returns(Some { Method = HttpMethod.Connect })
      TestCaseData("CUSTOM").Returns(Some { Method = HttpMethod.Parse "CUSTOM" })
      TestCaseData("").Returns(None)
      TestCaseData("   ").Returns(None)
      TestCaseData("INVALID METHOD").Returns(None) ]

[<TestCaseSource(nameof decodeCases)>]
let testDecode value = TryDecode<Method> value

let encodeCases =
    [ TestCaseData({ Method = HttpMethod.Get }).Returns("GET")
      TestCaseData({ Method = HttpMethod.Post }).Returns("POST")
      TestCaseData({ Method = HttpMethod.Put }).Returns("PUT")
      TestCaseData({ Method = HttpMethod.Delete }).Returns("DELETE")
      TestCaseData({ Method = HttpMethod.Connect }).Returns("CONNECT")
      TestCaseData({ Method = HttpMethod.Parse "CUSTOM" }).Returns("CUSTOM") ]

[<TestCaseSource(nameof encodeCases)>]
let testEncode (method: Method) =
    (method :> IFieldModel<Method>).Encode()

[<Test>]
let testMetadata () =
    let fld = FieldOf { Method = HttpMethod.Get }

    fld |> shouldEqual { Name = ":method"; Value = "GET" }
    Method.TryFind [ fld ] |> shouldEqual (Some { Method = HttpMethod.Get })
