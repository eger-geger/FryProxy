module FryProxy.HttpProxyServer

open System.IO
open System.Net
open System.Net.Sockets
open FryProxy.Http

let startServer (hostname: string, port: int) (handler) =
    async {
        let serverSocket = new Socket(SocketType.Stream, ProtocolType.Tcp)

        serverSocket.Bind(IPEndPoint(IPAddress.Parse(hostname), port))

        while true do
            let! socket = Async.AwaitTask(serverSocket.AcceptAsync())
            handler socket
    }

let proxyHttp (socket: Socket) =
    async {
        use stream = new NetworkStream(socket)

        let maybeHeader =
            stream
            |> Request.readHeaders
            |> Request.tryParseHeaders

        let resp =
            match maybeHeader with
            | Some header -> Stream.Null //TODO: connect to destination
            | None -> upcast Response.plainText 400us "FryProxy unable to parse request headers"

        do! resp.CopyToAsync(stream) |> Async.AwaitTask
    }
