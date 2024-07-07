namespace FryProxy

open System
open System.Net
open System.Net.Http
open System.Net.Sockets
open System.Threading
open System.Threading.Tasks
open FryProxy.IO
open Microsoft.FSharp.Core
open FryProxy.Http

/// HTTP proxy server accepting and handling the incoming request on a TCP socket.
type HttpProxy(handler: RequestHandlerChain, settings: Settings, tunnel: ITunnel) =

    let mutable workerTask = Task.CompletedTask

    let cancelSource = new CancellationTokenSource()

    let listener = new TcpListener(settings.Address, int settings.Port)

    let serve (socket: Socket) : unit =
        ignore
        <| task {
            let mutable tunneled = None
            use stack = new ResourceStack([ socket ])
            let ctx = Context(stack, handler, settings)

            let setupTunnel (server: Target) =
                task {
                    let! conn = tunnel.EstablishAsync(server, ctx)
                    tunneled <- Some conn
                }

            let tunneler (Message(Header({ Method = method }, _), _) as message) =
                if method = HttpMethod.Connect then
                    Proxy.tunnel setupTunnel message
                else
                    (ctx.CompleteChain ctx.ConnectAsync).Invoke message

            let clientStream =
                new AsyncTimeoutDecorator(new NetworkStream(socket, true)) |> stack.Push

            do! clientStream |> ctx.AllocateBuffer |> Proxy.respond tunneler

            match tunneled with
            | Some conn -> do! tunnel.RelayAsync(clientStream, conn, ctx)
            | None -> return ()
        }

    /// Proxy with opaque tunneling.
    new(setting) = new HttpProxy(RequestHandlerChain.Noop, setting, OpaqueTunnel())

    /// Proxy with opaque tunneling and default settings.
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
            backgroundTask {
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
