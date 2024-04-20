module FryProxy.HttpProxyServer

open System
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

// let proxyHttp (socket: Socket) =
//     async {
//         use stream = new NetworkStream(socket)
//
//         let maybeHeader =
//             stream
//             |> Request.readHeaders
//             |> Request.parseHeaders
//
//         let resp =
//             match maybeHeader with
//             | Some header -> Stream.Null //TODO: connect to destination
//             | None -> upcast Response.plainText 400us "FryProxy unable to parse request headers"
//
//         do! resp.CopyToAsync(stream) |> Async.AwaitTask
//     }
    
let makeRequest defaultPort (requestLine: HttpRequestLine, headers: HttpHeader list) (body: Stream) =
    let maybeHostPortPath =
        Request.tryResolveDestination(requestLine, headers)
        |> Option.map (Tuple.map2of3 (Option.defaultValue defaultPort))
        
    let host, port, path = maybeHostPortPath.Value
        
    use socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp)
    socket.Connect(host, port)
    
    use stream = new NetworkStream(socket, true)
    (Message.serializeHeaders (R requestLine) headers).CopyTo(stream)
    body.CopyTo(stream)
        
    ()