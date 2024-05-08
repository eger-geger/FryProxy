module FryProxy.Tests.Proxy.ProxyTests

open System.Net
open System.Net.Http
open System.Net.Http.Json
open System.Net.Sockets
open System.Threading.Tasks
open FsUnit

open FryProxy
open FryProxy.Tests.Proxy

open NUnit.Framework

type Request = HttpClient -> Task<HttpResponseMessage>

let startProxyListener () =
    let listener = new TcpListener(IPAddress.Loopback, 0)
    listener.Start()

    task {
        while listener.Server.IsBound do
            let! socket = listener.AcceptSocketAsync()
            Proxy.proxyHttp socket |> ignore
    }
    |> ignore

    listener

let listener = lazy startProxyListener ()

[<OneTimeSetUp>]
let setup () = listener.Force() |> ignore

[<OneTimeTearDown>]
let teardown = listener.Value.Dispose

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
let testSimpleProxy (request: Request) =
    let proxiedClient =
        new HttpClient(
            new HttpClientHandler(Proxy = WebProxy(listener.Value.LocalEndpoint.ToString())),
            BaseAddress = WiremockFixture.Uri
        )

    let plainClient = new HttpClient(BaseAddress = WiremockFixture.Uri)

    task {
        let! proxiedResponse = request proxiedClient
        let! plainResponse = request plainClient

        proxiedResponse |> should matchResponse plainResponse
    }
