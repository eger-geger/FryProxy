module FryProxy.Tests.Pipeline.Middleware.ConnectionTests

open System
open System.Net.Http
open System.Threading.Tasks
open FryProxy.Http
open FryProxy.Http.Fields
open FryProxy.Pipeline

open FsUnit
open NUnit.Framework

let request: RequestMessage =
    { Header =
        { StartLine = RequestLine.create11 HttpMethod.Get "http://example.org/"
          Fields = [] }
      Body = MessageBody.Empty }

let response = 200us |> Response.empty

let capture (captured: _ outref) req =
    captured <- req
    response |> ValueTask.FromResult

let closeConnectionCases =
    [ TestCaseData({ request with Header.Fields = [ Connection.CloseField ] }, [ Connection.CloseField ])
      TestCaseData({ request with Header.StartLine.Version = Version(1, 0) }, List.empty<Field>)
      TestCaseData(
          { request with
              Header.Fields = [ Connection.CloseField ]
              Header.StartLine.Version = Version(1, 0) },
          List.empty<Field>
      ) ]

[<TestCaseSource(nameof closeConnectionCases)>]
let ``should close connection`` req respFields =
    task {
        let closed = ref false
        let mutable capturedReq = Unchecked.defaultof<RequestMessage>

        let! resp = Middleware.clientConnection closed req (capture &capturedReq)

        closed.Value |> should equal true
        capturedReq |> should equal { req with Header.Fields = [] }

        resp
        |> should
            equal
            { response with
                Header.Fields = respFields
                Header.StartLine.version = req.Header.StartLine.Version }
    }

let keepConnectionCases =
    [ TestCaseData(request, List.empty<Field>)
      TestCaseData({ request with Header.Fields = [ Connection.KeepAliveField ] }, List.empty<Field>)
      TestCaseData(
          { request with
              Header.StartLine.Version = Version(1, 0)
              Header.Fields = [ Connection.KeepAliveField ] },
          [ Connection.KeepAliveField ]
      ) ]

[<TestCaseSource(nameof keepConnectionCases)>]
let ``should keep connection`` req respFields =
    task {
        let closed = ref false
        let mutable capturedReq = Unchecked.defaultof<RequestMessage>

        let! resp = Middleware.clientConnection closed req (capture &capturedReq)

        closed.Value |> should equal false
        capturedReq |> should equal { req with Header.Fields = [] }

        resp
        |> should
            equal
            { response with
                Header.Fields = respFields
                Header.StartLine.version = req.Header.StartLine.Version }
    }
