module FryProxy.Tests.Pipeline.Middleware.TunnelTests

open System
open System.IO
open System.Net
open System.Net.Http
open System.Threading.Tasks
open FryProxy.Http
open FryProxy.Pipeline

open FsUnit
open NUnit.Framework

[<Struct>]
type TunnelContext =
    val Host: string

    new(host) = { Host = host }

    interface Middleware.ITunnelAware<string, TunnelContext> with
        member this.Tunnel = this.Host |> ValueOption.ofObj
        member this.WithTunnel host = TunnelContext(host)

let connectReq host : RequestMessage =
    { Header = { StartLine = RequestLine.create11 HttpMethod.Connect host; Fields = [] }
      Body = MessageBody.Empty }

let handler _ =
    400us |> Response.empty |> ValueTask.FromResult

[<TestCase("example.com")>]
[<TestCase("example.com:8080")>]
let ``establishes tunnel and responds with OK`` (host: string) =
    task {
        let! resp, (ctx: TunnelContext) =
            Middleware.tunnel(sprintf "%O" >> ValueTask.FromResult)
            <| (connectReq host)
            <| (Handlers.initContext handler)

        ctx.Host |> should equal host
        resp |> should equal (Response.empty 200us)
    }

let errorStatusCodes =
    [ TestCaseData(IOException("Connection failed"), HttpStatusCode.BadGateway)
      TestCaseData(ArgumentException("Invalid port"), HttpStatusCode.InternalServerError) ]

[<TestCaseSource(nameof errorStatusCodes)>]
let ``responds with error when connection fails`` (err: exn, status: HttpStatusCode) =
    task {
        let! resp, (ctx: TunnelContext) =
            Middleware.tunnel(fun _ -> raise(err))
            <| (connectReq "example.com")
            <| (Handlers.initContext handler)

        ctx.Host |> should be Null
        resp |> should equal (Response.emptyConnectionClose status)
    }
