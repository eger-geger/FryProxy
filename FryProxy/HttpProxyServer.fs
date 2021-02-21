module FryProxy.HttpProxyServer

open System.Net
open System.Net.Sockets

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
        //        use reader = new UnbufferedStreamReader(stream)
        ()
    }


