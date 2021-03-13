module FryProxy.HttpProxyServer

open System.Net
open System.Net.Sockets
open FryProxy.Http.HttpMessage

let startServer (hostname: string, port: int) (handler) =
    async {
        let serverSocket =
            new Socket(SocketType.Stream, ProtocolType.Tcp)

        serverSocket.Bind(IPEndPoint(IPAddress.Parse(hostname), port))

        while true do
            let! socket = Async.AwaitTask(serverSocket.AcceptAsync())
            handler socket
    }

let proxyHttp (socket: Socket) =
    async {
        use stream = new NetworkStream(socket)
        use reader = new UnbufferedStreamReader(stream)
        
        match tryReadMessageHeader reader with
        | Some header -> () //TODO: connect to destination
        | None -> () //TODO: respond with 400
    }


