module FryProxy.Tests.Proxy.ProxyTests

open System
open System.Net
open System.Net.Http
open System.Net.Http.Json
open System.Net.Sockets
open System.Text
open System.Threading.Tasks


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

let transparentProxy =
    new HttpProxy(RequestHandlerChain.Noop, proxySettings, TransparentTunnel.DefaultFactory)

let opaqueProxy =
    new HttpProxy(RequestHandlerChain.Noop, proxySettings, OpaqueTunnel.Factory)

let makeProxiedClient baseAddress (port: int) =
    new HttpClient(
        new HttpClientHandler(
            Proxy = WebProxy("localhost", port),
            ServerCertificateCustomValidationCallback = HttpClientHandler.DangerousAcceptAnyServerCertificateValidator
        ),
        BaseAddress = baseAddress
    )

let opaqueProxyClient =
    lazy makeProxiedClient WiremockFixture.HttpUri opaqueProxy.Port

let opaqueProxySslClient =
    lazy makeProxiedClient WiremockFixture.HttpsUri opaqueProxy.Port

let transparentProxySslClient =
    lazy makeProxiedClient WiremockFixture.HttpsUri transparentProxy.Port

[<OneTimeSetUp>]
let setup () =
    transparentProxy.Start()
    opaqueProxy.Start()

[<OneTimeTearDown>]
let teardown () =
    let disposeLazy (value: #IDisposable Lazy) =
        if value.IsValueCreated then
            value.Value.Dispose()

    [ opaqueProxyClient; opaqueProxySslClient; transparentProxySslClient ]
    |> Seq.iter disposeLazy

    transparentProxy.Stop()
    opaqueProxy.Start()


let passingCases () : Request seq =
    let closing () =
        let msg = new HttpRequestMessage(HttpMethod.Get, "/example.org")
        msg.Headers.ConnectionClose <- true
        msg

    seq {
        yield (_.GetAsync("/example.org"))

        yield (_.SendAsync(closing()))

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

let assertEquivalentResponse (proxiedClient, request: Request) =
    task {
        let! proxiedResponse = request proxiedClient
        let! plainResponse = request WiremockFixture.HttpClient

        proxiedResponse |> should matchResponse plainResponse
    }

[<TestCaseSource(nameof passingCases); Parallelizable(ParallelScope.Self)>]
let testPlainReverseProxy (request: Request) =
    assertEquivalentResponse(opaqueProxyClient.Value, request)

[<TestCaseSource(nameof passingCases); Parallelizable(ParallelScope.Self)>]
let testTransparentSslReverseProxy (request: Request) =
    assertEquivalentResponse(transparentProxySslClient.Value, request)

[<TestCaseSource(nameof passingCases); Parallelizable(ParallelScope.Self)>]
let testOpaqueSslReverseProxy (request: Request) =
    assertEquivalentResponse(opaqueProxySslClient.Value, request)

[<Test; Timeout(10_000)>]
let testRequestTimeout () =
    task {
        use client = new TcpClient(AddressFamily.InterNetwork)
        do! client.ConnectAsync("localhost", transparentProxy.Port)

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

    let proxyClient = ProxyClient.executeRequest("localhost", transparentProxy.Port)

    task {
        let! Message(Header(status, _), _), _ = proxyClient request

        status.code |> should equal (uint16 HttpStatusCode.RequestTimeout)
    }

[<Test; Timeout(10_000)>]
let testGatewayTimeout () =
    let makeReq addr =
        let line = RequestLine.create11 HttpMethod.Get "/example.org"
        let fields = [ { Host = addr }.ToField() ]
        Message(Header(line, fields), Empty)

    let proxyClient = ProxyClient.executeRequest("localhost", transparentProxy.Port)

    task {
        use server = new TcpListener(IPAddress.Loopback, 0)

        server.Start()

        let! Message(Header(status, _), _), _ = server.LocalEndpoint.ToString() |> makeReq |> proxyClient

        status.code |> should equal (uint16 HttpStatusCode.GatewayTimeout)
    }

let invalidRequests () =
    seq {
        let line = RequestLine.create11 HttpMethod.Get "/example.org"
        let hostField = { Host = "localhost:8080" }.ToField()

        yield Message(Header(line, []), Empty)
        yield Message(Header(line, [ hostField; { Name = ""; Values = [ "hello" ] } ]), Empty)
        yield Message(Header(line, [ hostField; { Name = "X-ABC"; Values = [ "\n"; "\n" ] } ]), Empty)
    }

[<TestCaseSource(nameof invalidRequests)>]
let testBadRequest (request: RequestMessage) =
    let client = ProxyClient.executeRequest("localhost", transparentProxy.Port)

    task {
        let! Message(Header(status, _), _), _ = client request

        status.code |> should equal (uint16 HttpStatusCode.BadRequest)
    }

let invalidResponses () =
    seq {
        yield "HTTP/1.1 \n"
        yield "HTTP/1.1 200 OK"
        yield "HTTP/1.1 200 OK\n:A\n\n"
        yield "HTTP/1.1 200 OK\nX-H:A\n"
        yield "HTTP/1.1 200 OK\nHello\n\n"
    }

[<TestCaseSource(nameof invalidResponses)>]
let testBadGateway (response: string) =
    let makeReq addr =
        Message(Header(RequestLine.create11 HttpMethod.Get $"http://{addr}/", []), Empty)

    let client = ProxyClient.executeRequest("localhost", transparentProxy.Port)

    let respond (srv: TcpListener) =
        let binResp = response |> Encoding.ASCII.GetBytes |> ReadOnlyMemory

        task {
            use! client = srv.AcceptTcpClientAsync()
            do! client.GetStream().WriteAsync(binResp)
        }

    task {
        use server = new TcpListener(IPAddress.Loopback, 0)
        server.Start()
        respond server |> ignore

        let! Message(Header(status, _), _), _ = server.LocalEndpoint.ToString() |> makeReq |> client

        status.code |> should equal (uint16 HttpStatusCode.BadGateway)
    }
