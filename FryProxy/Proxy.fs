module FryProxy.Proxy

open System
open System.Buffers
open System.Net
open System.Net.Sockets
open System.Text
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

let startServer (host: string, port: int) handler =
    task {
        use! serverSocket = connectSocket (host, port)

        while true do
            let! clientSocket = serverSocket.AcceptAsync()
            handler clientSocket
    }

/// Copy chunked content from one stream to another
let copyChunked (buff: ReadBuffer) src dst : unit Task =
    task {
        let mutable lastChunk = false

        while not lastChunk do
            match! (buff, src) |> Parser.run Message.chunkHeaderParser with
            | Some header ->
                lastChunk <- header.Size = 0UL
                do! Message.writeChunkHeader dst header
                do! buff.Copy src dst header.Size
                
                match! (buff, src) |> Parser.run (Parser.commit utf8LineParser) with
                | Some rws when String.IsNullOrWhiteSpace rws ->
                    do! dst.WriteAsync(Encoding.UTF8.GetBytes(rws))
                | _ -> RequestError("Invalid chunk header") |> raise
            | None -> RequestError("Invalid chunk header") |> raise
    }


/// Transfer incoming request to remote server and copy the response back.
let exchangeWithRemote (buff: ReadBuffer) (line, headers, resource: Resource) clientStream : unit Task =
    task {
        use! serverSocket = connectSocket (resource.Host, resource.Port)
        use serverStream = new NetworkStream(serverSocket)

        do! Message.writeHeader (Request line, headers) serverStream

        let writeBody =
            match headers with
            | Message.FixedContent n -> buff.Copy clientStream serverStream n
            | Message.ChunkedContent -> copyChunked buff clientStream serverStream
            | _ -> Task.FromResult()

        do! writeBody

        do! serverStream.CopyToAsync clientStream
    }


let proxyHttp (clientSocket: Socket) =
    backgroundTask {
        use clientStream = new NetworkStream(clientSocket, true)
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
                "Proxy was unable to parse request message header"
                |> RequestError
                |> Task.FromException<unit>
                |> fun t _ -> t

        try
            do! respond clientStream
        with
        | RequestError msg -> do! Response.writePlainText (uint16 HttpStatusCode.BadRequest) msg clientStream
        | err -> do! Response.writePlainText (uint16 HttpStatusCode.InternalServerError) err.Message clientStream
    }
