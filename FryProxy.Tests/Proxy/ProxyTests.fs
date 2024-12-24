module FryProxy.Tests.Proxy.ProxyTests

open System
open System.Buffers
open System.Net
open System.Net.Http
open System.Net.Http.Headers
open System.Net.Http.Json
open System.Net.Sockets
open System.Text
open System.Threading.Tasks


open FryProxy.IO
open FsUnit
open Microsoft.FSharp.Core
open NUnit.Framework

open FryProxy
open FryProxy.IO.BufferedParser
open FryProxy.Http
open FryProxy.Http.Fields
open FryProxy.Pipeline
open FryProxy.Tests.Proxy
open FryProxy.Tests.Constraints

type Request = HttpClient -> Task<HttpResponseMessage>

let proxyTimeouts =
    SocketTimeouts(Read = TimeSpan.FromSeconds(5L), Write = TimeSpan.FromSeconds(10L))

let proxySettings =
    Settings(ClientTimeouts = proxyTimeouts, UpstreamTimeouts = proxyTimeouts)

let transparentProxy =
    new HttpProxy<DefaultContext>(RequestHandlerChain.Noop(), proxySettings, TransparentTunnel.DefaultFactory())

let opaqueProxy =
    new HttpProxy<DefaultContext>(RequestHandlerChain.Noop(), proxySettings, OpaqueTunnel.Factory)

let makeProxiedClient baseAddress (port: int) =
    new HttpClient(
        new HttpClientHandler(
            Proxy = WebProxy("localhost", port),
            ServerCertificateCustomValidationCallback = HttpClientHandler.DangerousAcceptAnyServerCertificateValidator
        ),
        BaseAddress = baseAddress
    )

[<Literal>]
let HttpBinPath = "/httpbin/post"

let opaqueProxyClient =
    lazy makeProxiedClient WiremockFixture.HttpUri opaqueProxy.Port

let opaqueProxySslClient =
    lazy makeProxiedClient WiremockFixture.HttpsUri opaqueProxy.Port

let transparentProxySslClient =
    lazy makeProxiedClient WiremockFixture.HttpsUri transparentProxy.Port

let makeViaHeader (proxy: _ HttpProxy) =
    ViaHeaderValue("1.1", proxy.Endpoint.ToString(), null, $"({proxySettings.Via.Comment})")

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
    seq {
        yield _.GetAsync("/example.org")

        yield _.PostAsJsonAsync(HttpBinPath, {| Name = "Fry" |})

        yield
            (fun client ->
                task {
                    use msg = new HttpRequestMessage(HttpMethod.Get, "/example.org")
                    msg.Headers.ConnectionClose <- true
                    return! client.SendAsync(msg)
                })

        yield
            (fun client ->
                task {
                    let content = JsonContent.Create({| Name = "Fry" |})
                    // enforce computing content length
                    do! content.LoadIntoBufferAsync()
                    return! client.PostAsync(HttpBinPath, content)
                })

        yield
            (fun client ->
                task {
                    use content = JsonContent.Create({| Name = "Fry" |})
                    do! content.LoadIntoBufferAsync()

                    use msg = new HttpRequestMessage(HttpMethod.Post, HttpBinPath, Content = content)
                    msg.Headers.ExpectContinue <- true

                    return! client.SendAsync(msg)
                })
    }

let assertEquivalentResponse via (proxiedClient, request: Request) =
    task {
        let! proxiedResponse = request proxiedClient
        let! plainResponse = request WiremockFixture.HttpClient

        if via <> null then
            plainResponse.Headers.Via.Add(via)

        proxiedResponse |> should matchResponse plainResponse
    }

[<TestCaseSource(nameof passingCases); Parallelizable(ParallelScope.Self)>]
let testPlainReverseProxy (request: Request) =
    assertEquivalentResponse (makeViaHeader opaqueProxy) (opaqueProxyClient.Value, request)

[<TestCaseSource(nameof passingCases); Parallelizable(ParallelScope.Self)>]
let testTransparentSslReverseProxy (request: Request) =
    assertEquivalentResponse (makeViaHeader transparentProxy) (transparentProxySslClient.Value, request)

[<TestCaseSource(nameof passingCases); Parallelizable(ParallelScope.Self)>]
let testOpaqueSslReverseProxy (request: Request) =
    assertEquivalentResponse null (opaqueProxySslClient.Value, request)

[<Test; Timeout(10_000)>]
let testRequestTimeout () =
    task {
        use client = new TcpClient(AddressFamily.InterNetwork)
        do! client.ConnectAsync("localhost", transparentProxy.Port)

        use cs = client.GetStream()

        let! { Header = { StartLine = status } } = Parser.runS Parse.response cs
        status.Code |> should equal (uint16 HttpStatusCode.RequestTimeout)
    }

[<Test; Timeout(10_000)>]
let testRequestTimeoutAfterReadingHeader () =
    let request =
        { Message.Header =
            { StartLine = RequestLine.create11 HttpMethod.Get "/example.org"
              Fields =
                [ FieldOf { Host = WiremockFixture.HttpUri.Authority }
                  FieldOf(ContentType.TextPlain Encoding.UTF8)
                  FieldOf { ContentLength = 128UL } ] }
          Body = Empty }

    let proxyClient = ProxyClient.executeRequest("localhost", transparentProxy.Port)

    task {
        let! { Header = { StartLine = status } }, _ = proxyClient request
        status.Code |> should equal (uint16 HttpStatusCode.RequestTimeout)
    }

[<Test; Timeout(10_000)>]
let testGatewayTimeout () =
    let makeReq addr : RequestMessage =
        { Header =
            { StartLine = RequestLine.create11 HttpMethod.Get "/example.org"
              Fields = [ FieldOf { Host = addr } ] }
          Body = Empty }

    let proxyClient = ProxyClient.executeRequest("localhost", transparentProxy.Port)

    task {
        use server = new TcpListener(IPAddress.Loopback, 0)

        server.Start()

        let! { Header = { StartLine = status } }, _ = server.LocalEndpoint.ToString() |> makeReq |> proxyClient

        status.Code |> should equal (uint16 HttpStatusCode.GatewayTimeout)
    }

let invalidRequests () : RequestMessage seq =
    let exampleGet: RequestMessage =
        { Header =
            { StartLine = RequestLine.create11 HttpMethod.Get "/example.org"
              Fields = [ FieldOf { Host = "localhost:8080" } ] }
          Body = MessageBody.Empty }

    let httpBinPost: RequestMessage =
        { Header =
            { StartLine = RequestLine.create11 HttpMethod.Post HttpBinPath
              Fields =
                [ FieldOf { Host = "localhost:8080" }
                  FieldOf { ContentType = "application/json" }
                  FieldOf { TransferEncoding = [ "chunked" ] } ] }
          Body = MessageBody.chunkedFromSeq [ { Header = { Size = 0UL; Extensions = [] }; Body = Trailer [] } ] }

    seq {
        yield Message.withoutField "Host" exampleGet
        yield Message.withField { Name = ""; Value = "hello" } exampleGet
        yield Message.withField { Name = "X-ABC"; Value = "\n, \n" } exampleGet

        yield
            { httpBinPost with
                Body =
                    MessageBody.chunkedFromSeq
                        [ { Header = { Size = 2UL; Extensions = [] }
                            Body = Content(MemoryByteSeq(Encoding.ASCII.GetBytes("hello world"))) }
                          { Header = { Size = 0UL; Extensions = [] }; Body = Trailer [] } ] }
    }

[<TestCaseSource(nameof invalidRequests)>]
let testBadRequest (request: RequestMessage) =
    let client = ProxyClient.executeRequest("localhost", transparentProxy.Port)

    task {
        let! resp, body = client request
        use _ = body

        let! content = MessageBody.read resp.Body
        do! TestContext.Error.WriteAsync(Encoding.ASCII.GetString(content.Span))

        resp.Header.StartLine.Code |> should equal (uint16 HttpStatusCode.BadRequest)
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
    let makeReq addr : RequestMessage =
        { Header =
            { StartLine = RequestLine.create11 HttpMethod.Get $"http://{addr}/"
              Fields = [] }
          Body = MessageBody.Empty }

    let client = ProxyClient.executeRequest("localhost", transparentProxy.Port)

    let respond (srv: TcpListener) =
        let binResp = response |> Encoding.ASCII.GetBytes |> ReadOnlyMemory

        task {
            use! client = srv.AcceptTcpClientAsync()
            use stream = client.GetStream()
            use buff = MemoryPool<byte>.Shared.Rent()

            let! _ = stream.ReadAsync(buff.Memory)
            do! stream.WriteAsync(binResp)
            do! stream.FlushAsync()
        }

    task {
        use server = new TcpListener(IPAddress.Loopback, 0)
        server.Start()
        respond server |> ignore

        let! resp, body = server.LocalEndpoint.ToString() |> makeReq |> client
        use _ = body

        let! content = MessageBody.read resp.Body
        do! TestContext.Out.WriteAsync(Encoding.ASCII.GetString(content.Span))

        resp.Header.StartLine.Code |> should equal (uint16 HttpStatusCode.BadGateway)
    }

[<Test>]
let testFailedExpectation () =
    task {
        use req = new HttpRequestMessage(HttpMethod.Post, HttpBinPath)
        req.Content <- new StringContent("Hello!")
        req.Headers.Expect.ParseAdd("unknown")

        let! response = opaqueProxyClient.Value.SendAsync(req)

        response.StatusCode |> should equal HttpStatusCode.ExpectationFailed
    }

[<Test>]
let testUnsupportedHttpVersion () =
    let client = ProxyClient.executeRequest("localhost", opaqueProxy.Port)

    let req: RequestMessage =
        { Header =
            { StartLine =
                { Version = Version(0, 9)
                  Method = HttpMethod.Get
                  Target = Uri(WiremockFixture.HttpUri, "/example.org").ToString() }
              Fields = [] }
          Body = MessageBody.Empty }

    task {
        let! { Header = { StartLine = status } }, body = client req
        use _ = body

        status.Code |> should equal 505us
    }
