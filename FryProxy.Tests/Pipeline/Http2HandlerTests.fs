module FryProxy.Tests.Pipeline.Http2HandlerTests

#nowarn "3535" "3536"

open System.Net.Http
open FryProxy.Http
open FryProxy.Http.Fields
open FryProxy.Http2.Fields
open FryProxy.Pipeline.Http2Handler
open NUnit.Framework

let noResult: (RequestLine * Field list) voption = ValueNone

let testCases =
    [ TestCaseData(
          [ FieldOf { Method = HttpMethod.Get }
            FieldOf { Authority = "example.com" }
            FieldOf { Path = "/index.html" }
            { Name = "User-Agent"; Value = "TestClient" } ]
      )
          .Returns(
              ValueSome(
                  { Method = HttpMethod.Get; Target = "/index.html"; Version = Protocol.Http2 },
                  [ { Name = "Host"; Value = "example.com" }
                    { Name = "User-Agent"; Value = "TestClient" } ]
              )
          )
          .SetName("parses standard GET request line with authority and path")

      TestCaseData(
          [ FieldOf { Method = HttpMethod.Connect }
            FieldOf { Authority = "example.com:443" } ]
      )
          .Returns(
              ValueSome(
                  { Method = HttpMethod.Connect
                    Target = "example.com:443"
                    Version = Protocol.Http2 },
                  [ { Name = "Host"; Value = "example.com:443" } ]
              )
          )
          .SetName("parses CONNECT request line with authority and omitted path")

      TestCaseData(
          [ FieldOf { Method = HttpMethod.Connect }
            FieldOf { Authority = "example.com:443" }
            FieldOf { Path = "/" } ]
      )
          .Returns(noResult)
          .SetName("fails to parse CONNECT request line when path is present")

      TestCaseData(
          [ FieldOf { Method = HttpMethod.Options }
            FieldOf { Authority = "example.com" } ]
      )
          .Returns(
              ValueSome(
                  { Method = HttpMethod.Options; Target = "*"; Version = Protocol.Http2 },
                  [ { Name = "Host"; Value = "example.com" } ]
              )
          )
          .SetName("parses OPTIONS request line with omitted path as asterisk target")

      TestCaseData(
          [ FieldOf { Method = HttpMethod.Options }
            FieldOf { Authority = "example.com" }
            FieldOf { Path = "*" } ]
      )
          .Returns(
              ValueSome(
                  { Method = HttpMethod.Options; Target = "*"; Version = Protocol.Http2 },
                  [ { Name = "Host"; Value = "example.com" } ]
              )
          )
          .SetName("parses OPTIONS request line with asterisk path")

      TestCaseData(
          [ FieldOf { Method = HttpMethod.Options }
            FieldOf { Authority = "example.com" }
            FieldOf { Path = "/api/v1/resource" } ]
      )
          .Returns(
              ValueSome(
                  { Method = HttpMethod.Options
                    Target = "/api/v1/resource"
                    Version = Protocol.Http2 },
                  [ { Name = "Host"; Value = "example.com" } ]
              )
          )
          .SetName("parses OPTIONS request line with specific resource path")

      TestCaseData([ FieldOf { Method = HttpMethod.Get }; FieldOf { Path = "/index.html" } ])
          .Returns(noResult)
          .SetName("fails to parse request line when authority is missing")

      TestCaseData([ FieldOf { Authority = "example.com" }; FieldOf { Path = "/index.html" } ])
          .Returns(noResult)
          .SetName("fails to parse request line when method is missing")

      TestCaseData([ FieldOf { Method = HttpMethod.Get }; FieldOf { Authority = "example.com" } ])
          .Returns(noResult)
          .SetName("fails to parse GET request line when path is missing") ]

[<TestCaseSource(nameof testCases)>]
let testTryDecodeRequestLine fields = tryDecodeRequestLine fields
