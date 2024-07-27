namespace FryProxy

open System
open System.Buffers
open System.Net
open System.Net.Sockets
open System.Threading
open System.Threading.Tasks

open Microsoft.FSharp.Core

open FryProxy.IO
open FryProxy.Http
open FryProxy.Extension

/// HTTP proxy server accepting and handling the incoming request on a TCP socket.
type HttpProxy(handler: RequestHandlerChain, settings: Settings, tunnelFactory: TunnelFactory) =

    let mutable workerTask = Task.CompletedTask

    let cancelSource = new CancellationTokenSource()

    let listener = new TcpListener(settings.Address, int settings.Port)

    let serve (clientBuff: ReadBuffer) =
        task {
            let tunnelRef = ref null
            let connCloseRef = ref false
            use stack = new ResourceStack()
            let sess = Session(stack, settings)

            let requestHandler =
                let makeTunnel target =
                    task {
                        let! srvBuff = sess.ConnectBufferAsync(target)
                        return! tunnelFactory.Invoke(target, clientBuff, srvBuff)
                    }

                RequestHandlerChain
                    .Join(Handlers.tunnel makeTunnel tunnelRef, handler)
                    .WrapOver(Handlers.connectionHeader connCloseRef)
                    .Seal(Proxy.reverse sess.ConnectBufferAsync)

            do! Proxy.respond requestHandler.Invoke clientBuff

            if tunnelRef.Value <> null then
                do! tunnelRef.Value.Invoke (handler, settings.ClientIdleTimeout)
                return false
            else
                return not connCloseRef.Value
        }

    let acceptConnection (socket: Socket) =
        socket.BufferSize <- settings.BufferSize
        socket.Timeouts <- settings.ClientTimeouts

        task {
            use sharedMem = MemoryPool.Shared.Rent(settings.BufferSize)
            use networkStream = new NetworkStream(socket, true)
            use timeoutStream = new AsyncTimeoutDecorator(networkStream)

            let clientBuff = ReadBuffer(sharedMem.Memory, timeoutStream)

            while! serve clientBuff do
                do! networkStream.WaitInputAsync settings.ClientIdleTimeout
        }

    /// Proxy with opaque tunneling.
    new(setting) = new HttpProxy(RequestHandlerChain.Noop, setting, OpaqueTunnel.Factory)

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
                    acceptConnection socket |> ignore
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
