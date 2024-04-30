module FryProxy.HttpProxyServer

open System.Buffers
open System.Net
open System.Net.Sockets
open System.Threading.Tasks
open FryProxy.Http
open FryProxy.IO
open FryProxy.IO.BufferedParser


/// Open socket to given host.
let connectSocket (host: string, port: int) =
    task {
        let socket = new Socket(SocketType.Stream, ProtocolType.Tcp)
        do! socket.ConnectAsync(IPEndPoint(IPAddress.Parse(host), port))
        return socket
    }

let startServer (host: string, port: int) handler =
    task {
        use! serverSocket = connectSocket (host, port)

        while true do
            let! clientSocket = serverSocket.AcceptAsync()
            handler clientSocket
    }

/// Transfer incoming request to remote server and copy the response back.
let exchangeWithRemote (buff: ReadBuffer) (line, headers, resource) clientStream : unit Task =
    task {
        use! serverSocket = connectSocket (resource.Host, resource.Port)
        use serverStream = new NetworkStream(serverSocket)
        
        do! Message.writeHeader (Request line, headers) serverStream
        do! buff.Copy clientStream serverStream
        do! serverStream.CopyToAsync clientStream
    }


let proxyHttp (clientSocket: Socket) =
    backgroundTask {
        use clientStream = new NetworkStream(clientSocket)
        use sharedMem = MemoryPool<byte>.Shared.Rent(4096)
        let buff = ReadBuffer(sharedMem.Memory)

        let! requestHeader = (buff, clientStream) |> Parser.run Request.requestHeaderParser

        let requestMetadata =
            requestHeader
            |> Option.bind (fun (line, headers) ->
                (line, headers)
                |> Request.tryResolveResource 80
                |> Option.map (fun resource -> line, headers, resource))

        let respond =
            match requestMetadata with
            | Some rmd -> exchangeWithRemote buff rmd
            | None ->
                Response.writePlainText
                <| (uint16 HttpStatusCode.BadRequest)
                <| "Proxy was unable to parse request message header"

        do! respond clientStream
    }
