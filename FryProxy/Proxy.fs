module FryProxy.Proxy

open System.Buffers
open System.Net
open System.Net.Sockets
open System.Threading.Tasks
open FryProxy.Http
open FryProxy.IO
open FryProxy.IO.BufferedParser


exception RequestError of string

/// Open socket to given host.
let connectSocket (host: string, port: int) =
    task {
        let socket = new Socket(SocketType.Stream, ProtocolType.Tcp)
        do! socket.ConnectAsync(IPEndPoint(IPAddress.Parse(host), port))
        return socket
    }

let badRequest = Response.writePlainText (uint16 HttpStatusCode.BadRequest)

let processChunks src dst =
    let copyParse = Message.copyChunk >> Parse.chunks >> Parser.run src

    task {
        match! copyParse dst with
        | Some _ -> return ()
        | None -> return! badRequest "Proxy unable to parse chunked content" dst
    }

let processRequest bodyType header (buff: ReadBuffer<_>) : unit Task =
    let proxyResource (res: Resource) =
        task {
            use! dstSocket = connectSocket (res.Host, res.Port)
            use dstStream = new NetworkStream(dstSocket)

            let line, fields = header
            do! Message.writeHeader (Request line, fields) dstStream

            let copyContent =
                match bodyType with
                | Empty -> ignore >> Task.FromResult
                | Content length -> buff.Copy length
                | Chunked -> processChunks buff

            do! copyContent dstStream
            do! dstStream.CopyToAsync buff.Stream
        }

    match Request.tryResolveResource 80 header with
    | Some res -> proxyResource res
    | None -> badRequest "Proxy unable to resolve destination resource" buff.Stream

let proxyHttp (clientSocket: Socket) =
    backgroundTask {
        use requestStream = new NetworkStream(clientSocket, true)
        use requestBuffer = MemoryPool<byte>.Shared.Rent(4096)
        let buff = ReadBuffer<NetworkStream>(requestBuffer.Memory, requestStream)

        match! Parse.request processRequest |> Parser.run buff with
        | Some _ -> ()
        | None -> return! badRequest "Proxy unable to parse request header" requestStream
    }
