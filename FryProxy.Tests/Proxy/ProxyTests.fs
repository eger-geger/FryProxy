module FryProxy.Tests.Proxy.ProxyTests

open System.Net
open System.Net.Http
open System.Net.Sockets
open System.Threading
open FsUnit

open FryProxy
open FryProxy.Tests.Proxy
open NUnit.Framework

let startProxy () =
    let listener = new TcpListener(IPAddress.Loopback, 0)
    listener.Start()

    task {
        TestContext.Out.WriteLine($"thread: {Thread.CurrentThread.Name}")

        while listener.Server.IsBound do
            let! socket = listener.AcceptSocketAsync()
            do! Proxy.proxyHttp socket
    }
    |> ignore

    listener



[<Test; Timeout(2000)>]
let testSimpleProxy () =
    use listener = startProxy ()
    let proxyEndpoint = listener.LocalEndpoint

    let httpClient =
        new HttpClient(new HttpClientHandler(Proxy = WebProxy(proxyEndpoint.ToString())))

    httpClient.BaseAddress <- WiremockFixture.Uri

    task {
        let! response = httpClient.GetStringAsync("/")

        response |> should not' (be EmptyString)
    }
