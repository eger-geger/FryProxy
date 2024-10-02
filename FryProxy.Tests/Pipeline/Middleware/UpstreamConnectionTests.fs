module FryProxy.Tests.Pipeline.Middleware.UpstreamConnectionTests

open System.Net.Http
open System.Threading.Tasks
open FryProxy.Http
open FryProxy.Http.Protocol
open FryProxy.Http.Fields
open FryProxy.Pipeline

open FsUnit
open NUnit.Framework

[<Struct>]
type ConCtx =
    val KeepAlive: bool

    new(keepAlive) = { KeepAlive = keepAlive }

    interface ConCtx Middleware.IUpstreamConnectionAware with
        member this.KeepUpstreamConnection = this.KeepAlive
        member this.WithKeepUpstreamConnection value = ConCtx(value)


let request: RequestMessage =
    { Header =
        { StartLine = RequestLine.create11 HttpMethod.Get "http://example.org/"
          Fields = [] }
      Body = MessageBody.Empty }

let responder ver fields _ =
    let empty = Response.empty 200us

    { empty with Header.StartLine.Version = ver; Header.Fields = fields }
    |> ValueTask.FromResult


let testCases =
    let empty = List.empty<Field>

    [ TestCaseData(Http10, empty).Returns(false)
      TestCaseData(Http11, empty).Returns(true)

      TestCaseData(Http10, [ Connection.CloseField ]).Returns(false)
      TestCaseData(Http11, [ Connection.CloseField ]).Returns(false)

      TestCaseData(Http10, [ Connection.KeepAliveField ]).Returns(true)
      TestCaseData(Http11, [ Connection.KeepAliveField ]).Returns(true) ]

[<TestCaseSource(nameof testCases)>]
let ``should detect keepAlive and drop connection field`` ver fields =
    let handler: ConCtx RequestHandler = RequestHandler.withContext(responder ver fields)

    task {
        let! resp, ctx = Middleware.upstreamConnection request handler

        resp.Header.Fields |> should be Empty

        return ctx.KeepAlive
    }
