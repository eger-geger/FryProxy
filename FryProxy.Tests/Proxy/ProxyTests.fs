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

let testCases () : Request seq =
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

[<TestCaseSource(nameof testCases)>]
let testPlainReverseProxy (request: Request) =
    assertEquivalentResponse(WiremockFixture.HttpUri, proxy.Port, request)


[<TestCaseSource(nameof testCases)>]
let testTransparentSslReverseProxy (request: Request) =
    assertEquivalentResponse(WiremockFixture.HttpsUri, proxy.Port, request)


[<TestCaseSource(nameof testCases)>]
let testOpaqueSslReverseProxy (request: Request) =
    assertEquivalentResponse(WiremockFixture.HttpsUri, opaqueProxy.Port, request)

[<Test; Timeout(10_000); Ignore("Socket timeouts do not apply to async methods")>]
let testRequestTimeout () =
    let requestLine = RequestLine.create11 HttpMethod.Get "/example.org"

    let fields =
        [ { Host = WiremockFixture.HttpUri.Authority }.ToField()
          ContentType.TextPlain(Encoding.UTF8).ToField()
          { ContentLength = 128UL }.ToField() ]

    let request = Message(Header(requestLine, fields), Empty)

    task {
        use bufMem = MemoryPool<byte>.Shared.Rent(1024)
        use client = new TcpClient(AddressFamily.InterNetwork)
        do! client.ConnectAsync("localhost", proxy.Port)

        use s = client.GetStream()
        do! Message.write request s

        let! Message(Header(status, _), _) = Parser.run (ReadBuffer(bufMem.Memory, s)) Parse.response

        status.code |> should equal HttpStatusCode.RequestTimeout
    }
