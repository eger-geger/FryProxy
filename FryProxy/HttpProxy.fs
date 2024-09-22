namespace FryProxy

open System
open System.Buffers
open System.IO
open System.Net
open System.Net.Sockets
open System.Threading
open System.Threading.Tasks

open FryProxy.Http.Fields
open Microsoft.FSharp.Core

open FryProxy.IO
open FryProxy.Http
open FryProxy.Pipeline
open FryProxy.Extension

/// Declares minimal set of information being propagated by proxy along with response message. 
type 'T IResponseContext when 'T: (new: unit -> 'T) and 'T :> IResponseContext<'T> =
    inherit Middleware.ITunnelAware<'T Tunnel, 'T>
    inherit Middleware.IClientConnectionAware<'T>
    inherit Middleware.IUpstreamConnectionAware<'T>

[<Struct>]
type DefaultContextState =
    { Tunnel: DefaultContext Tunnel
      CloseClientConnection: bool
      CloseUpstreamConnection: bool }

and [<Struct>] DefaultContext =
    val State: DefaultContextState

    new(state) = { State = state }

    interface IResponseContext<DefaultContext> with
        member this.Tunnel = this.State.Tunnel |> ValueOption.ofObj
        member this.KeepClientConnection = this.State.CloseClientConnection
        member this.KeepUpstreamConnection = this.State.CloseUpstreamConnection

        member this.WithKeepClientConnection value =
            DefaultContext { this.State with CloseClientConnection = value }

        member this.WithTunnel value =
            DefaultContext { this.State with Tunnel = value }

        member this.WithKeepUpstreamConnection value =
            DefaultContext { this.State with CloseUpstreamConnection = value }


/// HTTP proxy server accepting and handling the incoming request on a TCP socket.
type 'T HttpProxy when 'T: (new: unit -> 'T) and 'T :> IResponseContext<'T>
    (handler: 'T RequestHandlerChain, settings: Settings, tunnelFactory: 'T TunnelFactory) =
    let mutable workerTask = Task.CompletedTask
    let cancelSource = new CancellationTokenSource()
    let plainPool = new ConnectionPool(settings.BufferSize, settings.ServeIdleTimeout)
    let tunnelPool = new ConnectionPool(settings.BufferSize, settings.ServeIdleTimeout)
    let listener = new TcpListener(settings.Address, int settings.Port)

    let initPooledConn (ns: NetworkStream) : Stream =
        ns.Socket.Timeouts <- settings.UpstreamTimeouts
        new AsyncTimeoutDecorator(ns)

    let resolveEndpoint { Host = host; Port = port } =
        DnsEndPoint(host, port |> ValueOption.defaultValue settings.DefaultRequestPort)

    let establishPlainConnection target =
        let ep = resolveEndpoint target in plainPool.ConnectAsync(ep, initPooledConn >> ValueTask.FromResult)

    let establishTunnelConnection tunnelInit target =
        let port = target.Port |> ValueOption.defaultValue settings.DefaultRequestPort
        tunnelPool.ConnectAsync(DnsEndPoint(target.Host, port), initPooledConn >> tunnelInit)

    let establishTunnel clientBuff target =
        tunnelFactory.Invoke(establishTunnelConnection, target, clientBuff)

    let currentHop =
        lazy
            let name =
                if String.IsNullOrEmpty settings.Via.Name then
                    listener.LocalEndpoint.ToString()
                else
                    settings.Via.Name

            { Name = name; Comment = settings.Via.Comment; Protocol = "1.1" }

    // Handle a single client request and return flag indicating
    // whether connection can be reused for subsequent requests.
    let serve (clientBuff: ReadBuffer) =
        task {
            let tunnelMw = establishTunnel clientBuff |> Middleware.tunnel
            let handler = Middleware.viaField currentHop.Value +> handler

            let! ctx =
                Handlers.proxyHttpMessage
                <| establishPlainConnection
                <| (tunnelMw +> handler)
                <| clientBuff

            match ctx.Tunnel with
            | ValueSome tunnel ->
                do! tunnel.Invoke(handler, settings.ClientIdleTimeout)
                return false
            | ValueNone -> return ctx.KeepClientConnection
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
    new(setting) = new HttpProxy<_>(RequestHandlerChain.Noop(), setting, OpaqueTunnel.Factory)

    /// Proxy with opaque tunneling and default settings.
    new() = new HttpProxy<_>(Settings())

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

        plainPool.Stop()
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

                    try
                        plainPool.Stop()
                    with err ->
                        yield err
                }

            if not(Seq.isEmpty errors) then
                ("Failed to release underlying resources", errors)
                |> AggregateException
                |> raise
