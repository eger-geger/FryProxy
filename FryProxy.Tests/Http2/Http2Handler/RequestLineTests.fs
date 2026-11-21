module FryProxy.Tests.Pipeline.Http2.RequestLineTests

#nowarn "3535" "3536"

open System.Net.Http
open FryProxy.Http
open FryProxy.Http.Fields
open FryProxy.Http2.Fields
open FryProxy.Pipeline.Http2Handler
open NUnit.Framework


let decodingTestCases =
    let tc name (fields: Field list) (expected: (RequestLine * Field list) voption) =
        TestCaseData(fields, ExpectedResult = expected, TestName = name)

    let positive name fields requestLine requestFields =
        tc name fields (ValueSome(requestLine, requestFields))

    let negative name fields = tc name fields ValueNone

    [ positive "parses standard GET request line with authority and path"
      <| [ FieldOf { Method = HttpMethod.Get }
           FieldOf { Authority = "example.com" }
           FieldOf { Path = "/index.html" }
           { Name = "User-Agent"; Value = "TestClient" } ]
      <| { Method = HttpMethod.Get; Target = "/index.html"; Version = Protocol.Http2 }
      <| [ { Name = "Host"; Value = "example.com" }
           { Name = "User-Agent"; Value = "TestClient" } ]

      positive "parses CONNECT request line with authority and omitted path"
      <| [ FieldOf { Method = HttpMethod.Connect }
           FieldOf { Authority = "example.com:443" } ]
      <| { Method = HttpMethod.Connect
           Target = "example.com:443"
           Version = Protocol.Http2 }
      <| [ { Name = "Host"; Value = "example.com:443" } ]

      negative "fails to parse CONNECT request line when path is present"
      <| [ FieldOf { Method = HttpMethod.Connect }
           FieldOf { Authority = "example.com:443" }
           FieldOf { Path = "/" } ]

      positive "parses OPTIONS request line with omitted path as asterisk target"
      <| [ FieldOf { Method = HttpMethod.Options }
           FieldOf { Authority = "example.com" } ]
      <| { Method = HttpMethod.Options; Target = "*"; Version = Protocol.Http2 }
      <| [ { Name = "Host"; Value = "example.com" } ]

      positive "parses OPTIONS request line with asterisk path"
      <| [ FieldOf { Method = HttpMethod.Options }
           FieldOf { Authority = "example.com" }
           FieldOf { Path = "*" } ]
      <| { Method = HttpMethod.Options; Target = "*"; Version = Protocol.Http2 }
      <| [ { Name = "Host"; Value = "example.com" } ]

      positive "parses OPTIONS request line with specific resource path"
      <| [ FieldOf { Method = HttpMethod.Options }
           FieldOf { Authority = "example.com" }
           FieldOf { Path = "/api/v1/resource" } ]
      <| { Method = HttpMethod.Options
           Target = "/api/v1/resource"
           Version = Protocol.Http2 }
      <| [ { Name = "Host"; Value = "example.com" } ]

      negative "fails to parse request line when authority is missing"
      <| [ FieldOf { Method = HttpMethod.Get }; FieldOf { Path = "/index.html" } ]

      negative "fails to parse request line when method is missing"
      <| [ FieldOf { Authority = "example.com" }; FieldOf { Path = "/index.html" } ]

      negative "fails to parse GET request line when path is missing"
      <| [ FieldOf { Method = HttpMethod.Get }; FieldOf { Authority = "example.com" } ] ]

[<TestCaseSource(nameof decodingTestCases)>]
let testTryDecodeRequestLine fields = tryDecodeRequestLine fields
