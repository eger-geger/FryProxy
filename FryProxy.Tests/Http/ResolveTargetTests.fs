module FryProxy.Tests.Http.ResolveRequestTargetTests

open FryProxy.Http
open NUnit.Framework

let samples =
    let tc (line, fields, target: Target) =
        let line = RequestLine.tryDecode line |> ValueOption.get
        TestCaseData({ StartLine = line; Fields = fields }).Returns(target)

    let localhost8080 = { Host = "localhost"; Port = ValueSome 8080 }
    let exampleOrg = { Host = "example.org"; Port = ValueNone }

    Seq.map tc
    <| seq {
        yield "CONNECT localhost:8080 HTTP/1.1", [], localhost8080
        yield "PUT https://example.org/user HTTP/1.1", [], exampleOrg
        yield "DELETE https://localhost:8080/user HTTP/1.1", [], localhost8080
        yield "POST / HTTP/1.1", [ { Name = "Host"; Value = "example.org" } ], exampleOrg
        yield "OPTIONS * HTTP/1.1", [ { Name = "Host"; Value = "example.org" } ], exampleOrg
        yield "GET / HTTP/1.1", [ { Name = "Host"; Value = "localhost:8080" } ], localhost8080
    }

[<TestCaseSource(nameof samples)>]
let testResolveTarget (header: RequestHeader) =
    Request.tryResolveTarget header |> ValueOption.get
