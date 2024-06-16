module FryProxy.Tests.Proxy.ProxyTests

open System.Net
open System.Net.Http
open System.Net.Http.Json
open System.Security.Authentication
open System.Security.Cryptography.X509Certificates
open System.Threading.Tasks

open FsUnit
open NUnit.Framework

open FryProxy
open FryProxy.Tests.Proxy
open FryProxy.Tests.Constraints

type Request = HttpClient -> Task<HttpResponseMessage>


let cert = X509Certificate2.CreateFromCertFile("proxy-test.pfx")

let proxy = new HttpProxy(Settings(), SslAuthentication(cert, SslProtocols.None))

[<OneTimeSetUp>]
let setup () = proxy.Start()

[<OneTimeTearDown>]
let teardown () = proxy.Stop()

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


[<TestCaseSource(nameof testCases)>]
let testPlainReverseProxy (request: Request) =
    let baseAddress = WiremockFixture.HttpUri

    let proxiedClient =
        new HttpClient(new HttpClientHandler(Proxy = WebProxy("localhost", proxy.Port)), BaseAddress = baseAddress)

    let plainClient = new HttpClient(BaseAddress = baseAddress)

    task {
        let! proxiedResponse = request proxiedClient
        let! plainResponse = request plainClient

        proxiedResponse |> should matchResponse plainResponse
    }

[<TestCaseSource(nameof testCases)>]
let testSslReverseProxy (request: Request) =
    let baseAddress = WiremockFixture.HttpsUri
    let acceptCert = HttpClientHandler.DangerousAcceptAnyServerCertificateValidator

    let proxiedClient =
        new HttpClient(
            new HttpClientHandler(
                Proxy = WebProxy("localhost", proxy.Port),
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
