namespace FryProxy

open System
open System.Buffers
open System.Net
open System.Net.Sockets
open System.Threading
open System.Threading.Tasks
open FryProxy.IO
open FryProxy.Http
open Microsoft.FSharp.Core

type RequestHandler = delegate of request: RequestMessage * next: RequestHandler -> ResponseMessage ValueTask

type HttpProxy(settings: Settings) =

    let mutable workerTask = Task.CompletedTask

    let cancelSource = new CancellationTokenSource()

    let listener = new TcpListener(settings.Address, int settings.Port)

    let allocateBuffer (stack: ResourceStack) (socket: Socket) =
        let stream = new NetworkStream(socket, ownsSocket = true)
        stack.Push(stream :> IAsyncDisposable)

        let mem = MemoryPool.Shared.Rent(settings.BufferSize)
        stack.Push(mem)

        ReadBuffer(mem.Memory, stream)

    let connect (stack: ResourceStack) (host: string, port: int) =
        task {
            let upstream =
                new Socket(
                    SocketType.Stream,
                    ProtocolType.Tcp,
                    BufferSize = settings.BufferSize,
                    Timeouts = settings.UpstreamTimeouts
                )

            stack.Push(upstream)

            do! upstream.ConnectAsync(host, port)

            return allocateBuffer stack upstream
        }

    let handleConnection (socket: Socket) : unit =
        ignore
        <| task {
            use stack = ResourceStack()
            stack.Push(socket)

            let requestHandler = Proxy.executeRequest (connect stack) ValueTask.FromResult

            return! Proxy.proxyRequest requestHandler (allocateBuffer stack socket)
        }

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
                    handleConnection socket
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
