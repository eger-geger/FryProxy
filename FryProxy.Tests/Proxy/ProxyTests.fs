module FryProxy.Tests.Proxy.ProxyTests

open System
open System.Buffers
open System.Net
open System.Net.Http
open System.Net.Http.Json
open System.Net.Sockets
open System.Text
open System.Threading.Tasks


open FryProxy.IO
open FryProxy.IO.BufferedParser
open FsUnit
open Microsoft.FSharp.Core
open NUnit.Framework

open FryProxy
open FryProxy.Http
open FryProxy.Http.Fields
open FryProxy.Tests.Proxy
open FryProxy.Tests.Constraints

type Request = HttpClient -> Task<HttpResponseMessage>

let proxyTimeouts =
    SocketTimeouts(Read = TimeSpan.FromSeconds(5), Write = TimeSpan.FromSeconds(10))

let proxySettings =
    Settings(ClientTimeouts = proxyTimeouts, UpstreamTimeouts = proxyTimeouts)

let proxy =
    new HttpProxy(RequestHandlerChain.Noop, proxySettings, TransparentTunnel())

let opaqueProxy =
    new HttpProxy(RequestHandlerChain.Noop, proxySettings, OpaqueTunnel())

[<OneTimeSetUp>]
let setup () =
    proxy.Start()
    opaqueProxy.Start()

[<OneTimeTearDown>]
let teardown () =
    proxy.Stop()
    opaqueProxy.Start()

let passingCases () : Request seq =
    seq {
        yield (_.GetAsync("/example.org"))

        yield (_.PostAsJsonAsync("/httpbin/post", {| Name = "Fry" |}))

        yield
            (fun client ->
                task {
                    let content = JsonContent.Create({| Name = "Fry" |})
                    // enforce computing content length
                    do! content.LoadIntoBufferAsync()
                    return! client.PostAsync("/httpbin/post", content)
                })
    }

let assertEquivalentResponse (baseAddress, proxyPort: int, request: Request) =
    let acceptCert = HttpClientHandler.DangerousAcceptAnyServerCertificateValidator

    let proxiedClient =
        new HttpClient(
            new HttpClientHandler(
                Proxy = WebProxy("localhost", proxyPort),
                ServerCertificateCustomValidationCallback = acceptCert
            ),
            BaseAddress = baseAddress
        )

    let plainClient =
        new HttpClient(
            new HttpClientHandler(ServerCertificateCustomValidationCallback = acceptCert),
            BaseAddress = baseAddress
        )

    task {
        let! proxiedResponse = request proxiedClient
        let! plainResponse = request plainClient

        proxiedResponse |> should matchResponse plainResponse
    }

[<TestCaseSource(nameof passingCases)>]
let testPlainReverseProxy (request: Request) =
    assertEquivalentResponse(WiremockFixture.HttpUri, proxy.Port, request)


[<TestCaseSource(nameof passingCases)>]
let testTransparentSslReverseProxy (request: Request) =
    assertEquivalentResponse(WiremockFixture.HttpsUri, proxy.Port, request)


[<TestCaseSource(nameof passingCases)>]
let testOpaqueSslReverseProxy (request: Request) =
    assertEquivalentResponse(WiremockFixture.HttpsUri, opaqueProxy.Port, request)

[<Test; Timeout(10_000)>]
let testRequestTimeout () =
    task {
        use client = new TcpClient(AddressFamily.InterNetwork)
        do! client.ConnectAsync("localhost", proxy.Port)

        use cs = client.GetStream()

        let! Message(Header(status, _), _) = Parser.runS Parse.response cs
        status.code |> should equal (uint16 HttpStatusCode.RequestTimeout)
    }

[<Test; Timeout(10_000)>]
let testRequestTimeoutAfterReadingHeader () =
    let requestLine = RequestLine.create11 HttpMethod.Get "/example.org"

    let fields =
        [ { Host = WiremockFixture.HttpUri.Authority }.ToField()
          ContentType.TextPlain(Encoding.UTF8).ToField()
          { ContentLength = 128UL }.ToField() ]

    let request = Message(Header(requestLine, fields), Empty)

    let proxyClient = ProxyClient.executeRequest("localhost", proxy.Port)

    task {
        let! Message(Header(status, _), _) = proxyClient request

        status.code |> should equal (uint16 HttpStatusCode.RequestTimeout)
    }

[<Test; Timeout(10_000)>]
let testGatewayTimeout () =
    let makeReq addr =
        let line = RequestLine.create11 HttpMethod.Get "/example.org"
        let fields = [ { Host = addr }.ToField() ]
        Message(Header(line, fields), Empty)

    let proxyClient = ProxyClient.executeRequest("localhost", proxy.Port)

    task {
        use server = new TcpListener(IPAddress.Loopback, 0)

        server.Start()

        let! Message(Header(status, _), _) = server.LocalEndpoint.ToString() |> makeReq |> proxyClient

        status.code |> should equal (uint16 HttpStatusCode.GatewayTimeout)
    }

let invalidRequests =
    seq {
        let line = RequestLine.create11 HttpMethod.Get "/example.org"
        let hostField = { Host = "localhost:8080" }.ToField()

        yield Message(Header(line, []), Empty)
        yield Message(Header(line, [ hostField; { Name = ""; Values = [ "hello" ] } ]), Empty)
        yield Message(Header(line, [ hostField; { Name = "X-ABC"; Values = [ "\n"; "\n" ] } ]), Empty)
    }

[<TestCaseSource(nameof(invalidRequests))>]
let testInvalidRequest (request: RequestMessage) =
    let client = ProxyClient.executeRequest("localhost", proxy.Port)

    task {
        let! Message(Header(status, _), _) = client request

        status.code |> should equal (uint16 HttpStatusCode.BadRequest)
    }
