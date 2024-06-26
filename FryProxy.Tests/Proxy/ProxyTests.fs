module FryProxy.Tests.Proxy.ProxyTests

open System.Net
open System.Net.Http
open System.Net.Http.Json
open System.Threading.Tasks

open FsUnit
open NUnit.Framework

open FryProxy
open FryProxy.Tests.Proxy
open FryProxy.Tests.Constraints

type Request = HttpClient -> Task<HttpResponseMessage>

let proxy = new HttpProxy(RequestHandlerChain.Noop, Settings(), TransparentTunnel())

let opaqueProxy =
    new HttpProxy(RequestHandlerChain.Noop, Settings(), OpaqueTunnel())

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
