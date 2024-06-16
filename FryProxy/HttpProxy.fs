namespace FryProxy

open System
open System.Buffers
open System.Net
open System.Net.Http
open System.Net.Sockets
open System.Threading
open System.Threading.Tasks
open FryProxy.Http
open FryProxy.IO
open Microsoft.FSharp.Core


/// HTTP proxy server accepting and handling the incoming request on a TCP socket.
type HttpProxy(settings: Settings, auth: IAuthPolicy) =

    let mutable workerTask = Task.CompletedTask

    let cancelSource = new CancellationTokenSource()

    let listener = new TcpListener(settings.Address, int settings.Port)

    let allocateBuffer (stack: ResourceStack) stream =
        let mem = MemoryPool.Shared.Rent(settings.BufferSize) |> stack.Push in ReadBuffer(mem.Memory, stream)

    let connect (stack: ResourceStack) (res: Resource) =
        task {
            let socket =
                new Socket(
                    SocketType.Stream,
                    ProtocolType.Tcp,
                    BufferSize = settings.BufferSize,
                    Timeouts = settings.UpstreamTimeouts
                )
                |> stack.Push

            do! socket.ConnectAsync(res.Host, ValueOption.defaultValue settings.DefaultRequestPort res.Port)

            return new NetworkStream(socket, true) |> stack.Push
        }

    let serve (socket: Socket) : unit =
        let chain =
            if isNull settings.Handler then
                RequestHandlerChain.Noop
            else
                settings.Handler

        ignore
        <| task {
            let tunnel = ref<NetworkStream> null
            use stack = new ResourceStack([ socket ])
            let handlerChain buff = chain.Seal(Proxy.reverse buff)

            let buffConn (res: Resource) =
                task {
                    let! stream = connect stack res
                    return allocateBuffer stack stream
                }

            let buffTunnel (res: Resource) =
                task {
                    let! authStream = auth.AuthServer(res.Host, tunnel.Value)
                    return authStream |> stack.Push |> allocateBuffer stack
                }

            let createTunnel (res: Resource) =
                task {
                    let! stream = connect stack res
                    tunnel.Value <- stream
                }

            let handleConnect (Message(Header({ method = method }, _), _) as message) =
                if method = HttpMethod.Connect then
                    Proxy.tunnel createTunnel message
                else
                    (handlerChain buffConn).Invoke message

            let clientStream = new NetworkStream(socket, true) |> stack.Push

            do! clientStream |> allocateBuffer stack |> Proxy.respond handleConnect

            if (isNull >> not) tunnel.Value then
                let! authStream = auth.AuthClient(clientStream)

                do!
                    authStream
                    |> stack.Push
                    |> allocateBuffer stack
                    |> Proxy.respond (handlerChain buffTunnel).Invoke
        }

    /// Unauthenticated proxy.
    new(setting) = new HttpProxy(setting, Unauthenticated())

    /// Unauthenticated proxy with default settings.
    new() = new HttpProxy(Settings())

    /// Endpoint proxy listens on.
    member _.Endpoint = listener.LocalEndpoint

    /// Port number proxy listens on.
    member _.Port = (listener.LocalEndpoint :?> IPEndPoint).Port

    /// Whether it is still listening on the underlying port.
    member _.IsListening = listener.Server.IsBound

    /// Whether it has started, listening and not stopped yet.
    member this.IsRunning = not cancelSource.IsCancellationRequested && this.IsListening

    /// Start accepting requests.
    member _.Start() =
        listener.Start(int settings.BacklogSize)

        workerTask <-
            task {
                while not cancelSource.IsCancellationRequested do
                    let! socket = listener.AcceptSocketAsync(cancelSource.Token)
                    socket.BufferSize <- settings.BufferSize
                    socket.Timeouts <- settings.ClientTimeouts
                    serve socket
            }

    /// Attempt to shut down gracefully within given timeout.
    member _.Stop(timeout: TimeSpan) =
        cancelSource.Cancel()

        try
            workerTask.Wait(timeout) |> ignore
        with
        | :? AggregateException
        | :? ObjectDisposedException -> ()

        listener.Stop()

    /// Graceful shut down with 5 seconds timeout.
    member this.Stop() = this.Stop(TimeSpan.FromSeconds(5))

    interface IDisposable with
        override _.Dispose() =
            let errors =
                seq {
                    try
                        cancelSource.Cancel()
                        cancelSource.Dispose()
                    with err ->
                        yield err

                    try
                        workerTask.Dispose()
                    with err ->
                        yield err

                    try
                        listener.Dispose()
                    with err ->
                        yield err
                }

            if not(Seq.isEmpty errors) then
                ("Failed to release underlying resources", errors)
                |> AggregateException
                |> raise
