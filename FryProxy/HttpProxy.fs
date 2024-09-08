﻿namespace FryProxy

open System
open System.Buffers
open System.IO
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
    let plainPool = new ConnectionPool(settings.BufferSize, settings.ServeIdleTimeout)
    let tunnelPool = new ConnectionPool(settings.BufferSize, settings.ServeIdleTimeout)
    let listener = new TcpListener(settings.Address, int settings.Port)

    let initPooledConn (ns: NetworkStream) : Stream =
        ns.Socket.Timeouts <- settings.UpstreamTimeouts
        new AsyncTimeoutDecorator(ns)

    let resolveEndpoint { Host = host; Port = port } =
        DnsEndPoint(host, port |> ValueOption.defaultValue settings.DefaultRequestPort)

    let establishPlainConnection (ref: IConnection ref) target =
        ValueTask.FromTask
        <| task {
            let! conn = plainPool.ConnectAsync(resolveEndpoint target, initPooledConn >> ValueTask.FromResult)
            ref.Value <- conn
            return conn.Buffer
        }

    let establishTunnelConnection tunnelInit target =
        let port = target.Port |> ValueOption.defaultValue settings.DefaultRequestPort
        tunnelPool.ConnectAsync(DnsEndPoint(target.Host, port), initPooledConn >> tunnelInit)

    // Handle a single client request and return flag indicating
    // whether connection can be reused for subsequent requests.
    let serve (clientBuff: ReadBuffer) =
        task {
            let tunnelRef = ref null
            let closeClientConn = ref false
            let closeUpstreamConn = ref false
            let upstreamConn = ref Connection.Empty

            use _ =
                { new IDisposable with
                    member _.Dispose() =
                        use conn = upstreamConn.Value

                        if closeUpstreamConn.Value then
                            conn.Close() }


            let establishTunnel target =
                tunnelFactory.Invoke(establishTunnelConnection, target, clientBuff)

            let requestHandler =
                RequestHandlerChain
                    .Join(Handlers.tunnel establishTunnel tunnelRef, handler)
                    .WrapOver(Handlers.requestConnectionHeader closeClientConn)
                    .WrapOver(Handlers.responseConnectionHeader closeUpstreamConn)
                    .Seal(Proxy.reverse(establishPlainConnection upstreamConn))

            do! Proxy.respond requestHandler.Invoke clientBuff

            if tunnelRef.Value <> null then
                do! tunnelRef.Value.Invoke(handler, settings.ClientIdleTimeout)
                return false
            else
                return not closeClientConn.Value
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
