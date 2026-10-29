module FryProxy.Tests.Proxy.ProxyTests

open System
open System.Buffers
open System.IO
open System.Net
open System.Net.Http
open System.Net.Http.Headers
open System.Net.Http.Json
open System.Net.Sockets
open System.Text
open System.Threading
open System.Threading.Tasks
open DotNet.Testcontainers.Builders
open DotNet.Testcontainers.Configurations


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

let makeHttp2ProxiedClient baseAddress (port: int) =
    let handler = new SocketsHttpHandler()

    handler.ConnectCallback <-
        Func<SocketsHttpConnectionContext, CancellationToken, ValueTask<Stream>>(fun _ ct ->
            ValueTask<Stream>(
                task {
                    let tcp = new TcpClient()
                    do! tcp.ConnectAsync(IPAddress.Loopback, port, ct)
                    return tcp.GetStream() :> Stream
                }
            ))

    new HttpClient(
        handler,
        BaseAddress = baseAddress,
        DefaultRequestVersion = HttpVersion.Version20,
        DefaultVersionPolicy = HttpVersionPolicy.RequestVersionExact
    )

let opaqueProxyHttp2Client =
    lazy makeHttp2ProxiedClient WiremockFixture.HttpUri opaqueProxy.Port

let makeViaHeader (proxy: _ HttpProxy) =
    ViaHeaderValue("1.1", proxy.Endpoint.ToString(), null, $"({proxySettings.Via.Comment})")

[<OneTimeSetUp>]
let setup () =
    transparentProxy.Start()
    opaqueProxy.Start()
    // Expose the proxy port for h2spec containers to connect
    TestcontainersSettings.ExposeHostPortsAsync(uint16 opaqueProxy.Port, CancellationToken.None).Wait()

[<OneTimeTearDown>]
let teardown () =
    let disposeLazy (value: #IDisposable Lazy) =
        if value.IsValueCreated then
            value.Value.Dispose()

    [ opaqueProxyClient
      opaqueProxySslClient
      transparentProxySslClient
      opaqueProxyHttp2Client ]
    |> Seq.iter disposeLazy

    transparentProxy.Stop()
    opaqueProxy.Stop()


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

[<Test; CancelAfter(10_000)>]
let testRequestTimeout ct =
    task {
        use client = new TcpClient(AddressFamily.InterNetwork)
        do! client.ConnectAsync("localhost", transparentProxy.Port, ct)

        use cs = client.GetStream()

        let! { Header = { StartLine = status } } = Parser.runS Parse.response cs
        status.Code |> should equal (uint16 HttpStatusCode.RequestTimeout)
    }

[<Test; CancelAfter(10_000)>]
let testRequestTimeoutAfterReadingHeader ct =
    let request =
        { Message.Header =
            { StartLine = RequestLine.create11 HttpMethod.Get "/example.org"
              Fields =
                [ FieldOf { Host = WiremockFixture.HttpUri.Authority }
                  FieldOf(ContentType.TextPlain Encoding.UTF8)
                  FieldOf { ContentLength = 128UL } ] }
          Body = Empty }

    let proxyClient = ProxyClient.executeRequest ct ("localhost", transparentProxy.Port)

    task {
        let! { Header = { StartLine = status } }, _ = proxyClient request
        status.Code |> should equal (uint16 HttpStatusCode.RequestTimeout)
    }

[<Test; CancelAfter(10_000)>]
let testGatewayTimeout ct =
    let makeReq addr : RequestMessage =
        { Header =
            { StartLine = RequestLine.create11 HttpMethod.Get "/example.org"
              Fields = [ FieldOf { Host = addr } ] }
          Body = Empty }

    let proxyClient = ProxyClient.executeRequest ct ("localhost", transparentProxy.Port)

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
                            Body = Content(MemoryByteSeq("hello world"B)) }
                          { Header = { Size = 0UL; Extensions = [] }; Body = Trailer [] } ] }
    }

[<TestCaseSource(nameof invalidRequests); CancelAfter(2_000)>]
let testBadRequest (request: RequestMessage) ct =
    let client = ProxyClient.executeRequest ct ("localhost", transparentProxy.Port)

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

[<TestCaseSource(nameof invalidResponses); CancelAfter(1_000)>]
let testBadGateway (response: string) ct =
    let makeReq addr : RequestMessage =
        { Header =
            { StartLine = RequestLine.create11 HttpMethod.Get $"http://{addr}/"
              Fields = [] }
          Body = MessageBody.Empty }

    let client = ProxyClient.executeRequest ct ("localhost", transparentProxy.Port)

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

[<Test; CancelAfter(1_000)>]
let testUnsupportedHttpVersion ct =
    let client = ProxyClient.executeRequest ct ("localhost", opaqueProxy.Port)

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

[<Test; CancelAfter(5_000)>]
let testHttp2ProxyGet (ct: CancellationToken) =
    task {
        use! response = opaqueProxyHttp2Client.Value.GetAsync("/example.org", ct)
        response.Version |> should equal HttpVersion.Version20
        response.StatusCode |> should equal HttpStatusCode.OK

        let! content = response.Content.ReadAsStringAsync(ct)
        content |> should not' (be EmptyString)
    }

[<Test; CancelAfter(5_000)>]
let testHttp2ProxyPost (ct: CancellationToken) =
    assertEquivalentResponse
        (makeViaHeader opaqueProxy)
        (opaqueProxyHttp2Client.Value, _.PostAsJsonAsync(HttpBinPath, {| Name = "Fry" |}, ct))

let runH2SpecGroup (testGroup: string) (ct: CancellationToken) =
    task {
        let safeName = testGroup.Replace("/", "_").Replace(" ", "_")
        let containerReportPath = $"/tmp/h2spec-{safeName}.xml"
        let h2SpecReportDir = Path.Combine(TestContext.CurrentContext.WorkDirectory, "h2spec")
        let hostReportPath = Path.Combine(h2SpecReportDir, $"h2spec-{safeName}.xml")

        let container =
            ContainerBuilder("summerwind/h2spec:latest")
                .WithCommand(
                    testGroup,
                    "--host", "host.testcontainers.internal",
                    "--port", string opaqueProxy.Port,
                    "--path", "/h2spec",
                    "--timeout", "1",
                    "--junit-report", containerReportPath
                )
                .Build()

        do! container.StartAsync(ct)

        let! exitCode = container.GetExitCodeAsync()
        let! stdout, stderr = container.GetLogsAsync()

        do! TestContext.Out.WriteAsync(stdout)
        do! TestContext.Error.WriteAsync(stderr)

        Directory.CreateDirectory(h2SpecReportDir) |> ignore
        try
            let! reportBytes = container.ReadFileAsync(containerReportPath, ct)
            do! File.WriteAllBytesAsync(hostReportPath, reportBytes, ct)
            do! TestContext.Out.WriteAsync($"JUnit report saved to {hostReportPath}")
        with
        | :? FileNotFoundException ->
            do! TestContext.Out.WriteAsync($"Warning: No JUnit report found at {containerReportPath}")
        | ex ->
            do! TestContext.Out.WriteAsync($"Warning: Failed to copy JUnit report: {ex.Message}")

        do! container.StopAsync(ct)

        Assert.AreEqual(0L, exitCode, $"h2spec test group '{testGroup}' failed with exit code {exitCode}")
    }

// h2spec conformance tests grouped by protocol area

[<Test; Explicit; Category("h2spec"); Parallelizable(ParallelScope.Self); CancelAfter(300_000)>]
[<TestCase("generic/1 generic/2")>]
[<TestCase("generic/3.1 generic/3.2 generic/3.3")>]
[<TestCase("generic/3.4 generic/3.5 generic/3.7 generic/3.8 generic/3.9 generic/3.10")>]
[<TestCase("generic/4 generic/5")>]
[<TestCase("http2/3 http2/3.5 http2/4.1 http2/4.2 http2/4.3")>]
[<TestCase("http2/5.1 http2/5.1.1 http2/5.1.2 http2/5.3 http2/5.3.1 http2/5.4 http2/5.4.1 http2/5.5")>]
[<TestCase("http2/6.1 http2/6.2 http2/6.3 http2/6.4 http2/6.5 http2/6.5.2 http2/6.5.3 http2/6.7 http2/6.8 http2/6.9 http2/6.9.1 http2/6.9.2 http2/6.10")>]
[<TestCase("http2/7 http2/8.1 http2/8.1.2 http2/8.1.2.1 http2/8.1.2.2 http2/8.1.2.3 http2/8.1.2.6 http2/8.2")>]
[<TestCase("hpack/2 hpack/4 hpack/5 hpack/6")>]
let ``h2spec conformance test`` (testGroup: string, ct: CancellationToken) =
    runH2SpecGroup testGroup ct
