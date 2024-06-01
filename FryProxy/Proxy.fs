module FryProxy.Proxy

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

let badRequest =
    Response.plainText (uint16 HttpStatusCode.BadRequest) >> Task.FromResult

let proxyResponse rb =
    task {
        try
            return! Parse.response |> Parser.run rb
        with ParseError m ->
            return! badRequest $"Proxy unable to parse response headers: {m}"
    }

let proxyRequest buffer (Message(header, _) as message) =
    let proxyResource (res: Resource) =
        task {
            let! socket = connectSocket (res.Host, res.Port)
            let stream = new NetworkStream(socket, true)

            do! Message.write message stream

            return! proxyResponse (buffer stream)
        }

    match Request.tryResolveResource 80 header with
    | Some res -> proxyResource res
    | None -> badRequest "Proxy unable to resolve destination resource"

let proxyHttp (clientSocket: Socket) =
    backgroundTask {
        use requestStream = new NetworkStream(clientSocket, true)
        use requestBuffer = MemoryPool<byte>.Shared.Rent(4096)
        use responseBuffer = MemoryPool<byte>.Shared.Rent(4096)
        let rb = ReadBuffer<NetworkStream>(requestBuffer.Memory, requestStream)

        let respond =
            task {
                try
                    let! request = Parse.request |> Parser.run rb
                    return! proxyRequest (fun s -> ReadBuffer(responseBuffer.Memory, s)) request
                with ParseError m ->
                    return! badRequest $"Proxy unable to parse request header: {m}"
            }

        let! response = respond

        do! Message.write response requestStream
    }
