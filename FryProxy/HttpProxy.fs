namespace FryProxy

open System
open System.Net
open System.Net.Http
open System.Net.Sockets
open System.Threading
open System.Threading.Tasks

open Microsoft.FSharp.Core

open FryProxy.IO
open FryProxy.Http
open FryProxy.Extension

/// HTTP proxy server accepting and handling the incoming request on a TCP socket.
type HttpProxy(handler: RequestHandlerChain, settings: Settings, tunnel: ITunnel) =

    let mutable workerTask = Task.CompletedTask

    let cancelSource = new CancellationTokenSource()

    let listener = new TcpListener(settings.Address, int settings.Port)

    let handleConnect cxnF (cxn: _ ref) req (next: RequestHandler) : ResponseMessage ValueTask =
        let (Message(Header({ Method = method }, _) as header, _)) = req

        if method = HttpMethod.Connect then
            ValueTask.FromTask
            <| task {
                let! resp, cxnOpt = Proxy.tunnel cxnF header

                match cxnOpt with
                | ValueSome v -> cxn.Value <- v
                | ValueNone -> cxn.Value <- null

                return resp
            }
        else
            next.Invoke req

    let serve (socket: Socket) : unit =
        ignore
        <| task {
            let tunnelRef = { contents = null }
            use stack = new ResourceStack([ socket ])
            let sess = Session(stack, handler, settings)

            let requestHandler =
                let makeTunnel target = tunnel.EstablishAsync(target, sess)
                
                RequestHandlerChain
                    .Join(handleConnect makeTunnel tunnelRef, handler)
                    .Seal(Proxy.reverse sess.ConnectBufferAsync)

            let clientStream =
                new AsyncTimeoutDecorator(new NetworkStream(socket, true)) |> stack.Push

            do! clientStream |> sess.AllocateBuffer |> Proxy.respond requestHandler.Invoke

            match tunnelRef with
            | { contents = null } -> return ()
            | { contents = conn } -> do! tunnel.RelayAsync(clientStream, conn, sess)
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
