module FryProxy.ProxyClient

open System.Net.Sockets
open FryProxy.Http
open FryProxy.IO.BufferedParser

/// Run HTTP request through proxy and return HTTP response.
let executeRequest (host: string, port) (request: RequestMessage) =
    task {
        use client = new TcpClient(AddressFamily.InterNetwork)
        do! client.ConnectAsync(host, port)

        let cs = client.GetStream()
        do! Message.write request cs
        return! Parser.runS Parse.response cs
    }
