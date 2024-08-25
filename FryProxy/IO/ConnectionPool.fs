namespace FryProxy.IO

open Unchecked
open System
open System.Buffers
open System.Collections.Concurrent
open System.IO
open System.Net
open System.Net.Sockets

open System.Threading
open System.Threading.Tasks
open FryProxy.Extension
open Microsoft.FSharp.Core

/// Potentially reusable buffered connection, disposing which does not necessary equivalent to closing.
type IConnection =
    inherit IDisposable

    /// Read buffer wrapping a connection stream.
    abstract member Buffer: ReadBuffer

    /// Close the connection freeing the underlying resources.
    abstract member Close: unit -> unit

/// Gives temporary exclusive ownership over the pooled connection.
type internal ScopedConnection(name: string, rb: ReadBuffer, clear: bool -> unit) =
    let mutable closed = false

    /// Execute a callback when lease ends or connection closes.
    /// Callback accepts a flag indicating whether connection will be closed.
    member _.OnDispose fn =
        let inline combine close =
            do clear close
            do fn close

        new ScopedConnection(name, rb, combine)

    override _.ToString() = $"scoped({name})"

    interface IConnection with
        member _.Buffer = rb

        member _.Close() =
            closed <- true
            clear(true)

        member _.Dispose() =
            if not closed then
                clear(closed)

type internal PooledConnection(ownedMem: byte IMemoryOwner, socket: Socket, stream: Stream) =
    let name = $"{socket.LocalEndPoint}->{socket.RemoteEndPoint}"
    let rb = lazy ReadBuffer(ownedMem.Memory, stream)
    let mutable idleFrom = DateTime.UtcNow.Ticks

    let clearLease (conn: PooledConnection) close =
        if Interlocked.Exchange(&idleFrom, DateTime.UtcNow.Ticks) = 0 && close then
            conn.Close()

    /// For how long connection remained idle.
    member _.IdleDuration =
        if idleFrom <> 0 then
            DateTime.UtcNow - DateTime(idleFrom, DateTimeKind.Utc)
        else
            TimeSpan.Zero

    /// Gain temporary exclusive ownership over the connection.
    member this.Lease() =
        if Interlocked.Exchange(&idleFrom, 0) = 0 then
            invalidOp $"{this} lease already taken"
        else
            new ScopedConnection(name, rb.Value, clearLease this)

    /// Close underlying socket and release other connection resources.
    member this.Close() =
        do Exception.Ignore stream.Dispose
        do Exception.Ignore socket.Dispose
        do Exception.Ignore ownedMem.Dispose

    override _.ToString() =
        let status =
            if idleFrom = 0 then
                "active"
            else
                $"passive[{DateTime(idleFrom, DateTimeKind.Utc)}]"

        $"{status}({name})"

[<Struct>]
type internal PoolQueue =
    { Timer: Timer
      Queue: PooledConnection ConcurrentQueue }

    static member New(conn, state, handler) =
        { Queue = ConcurrentQueue([ conn ])
          Timer = new Timer(handler, state, Timeout.Infinite, Timeout.Infinite) }

/// Pool of outgoing TCP connections paired with a read buffer. Releases passive connection after a timeout.
type ConnectionPool(bufferSize: int, timeout: TimeSpan) =
    let stopping = new CancellationTokenSource()
    let connections = ConcurrentDictionary<IPEndPoint, PoolQueue>()

    let connect (ip: IPEndPoint) =
        task {
            let memLease = MemoryPool.Shared.Rent(bufferSize)

            try
                let socket = new Socket(SocketType.Stream, ProtocolType.Tcp)
                socket.ReceiveBufferSize <- bufferSize

                do! socket.ConnectAsync(ip, stopping.Token)

                return PooledConnection(memLease, socket, new NetworkStream(socket, true))
            with ex ->
                memLease.Dispose()
                return ex.Rethrow()
        }

    let clear { Queue = q; Timer = t } =
        Exception.Ignore t.Dispose
        q |> Seq.iter(_.Close())

    let reschedule { Queue = q; Timer = t } =
        let mutable next = defaultof<_>

        let delay =
            if q.TryPeek(&next) then
                timeout - next.IdleDuration
            else
                timeout

        t.Change(max delay TimeSpan.Zero, Timeout.InfiniteTimeSpan) |> ignore

    let dequeue (state: obj) =
        let ip = state :?> IPEndPoint
        let mutable pool = defaultof<_>
        let mutable conn = defaultof<_>

        if connections.TryGetValue(ip, &pool) && pool <> defaultof<_> then
            if pool.Queue.TryDequeue(&conn) then
                do conn.Close()

            if pool.Queue.IsEmpty && connections.TryRemove(ip, &pool) then
                do clear pool
            else
                do reschedule pool

    let enqueue ip (conn: PooledConnection) close =
        if stopping.IsCancellationRequested then
            do conn.Close()
        else if not close then
            let inline add _ = PoolQueue.New(conn, ip, dequeue)
            let inline upd _ ({ Queue = q } as pool) = let _ = q.Enqueue conn in pool
            connections.AddOrUpdate(ip, add, upd) |> reschedule

    new(bufferSize) = new ConnectionPool(bufferSize, TimeSpan.FromSeconds(30))

    /// Acquire a temporary ownership over new or pooled connection, which is returned back to the pool when lease
    /// ends unless it was closed.
    member _.ConnectAsync(ip: IPEndPoint) : IConnection ValueTask =
        let mutable pool = defaultof<_>
        let mutable head = defaultof<_>

        if stopping.IsCancellationRequested then
            ValueTask.FromCanceled<_>(stopping.Token)
        else if connections.TryGetValue(ip, &pool) && pool.Queue.TryDequeue(&head) then
            do reschedule pool
            ValueTask.FromResult(head.Lease().OnDispose(enqueue ip head))
        else
            ValueTask.FromTask
            <| task {
                let! conn = connect ip
                return conn.Lease().OnDispose(enqueue ip conn) :> IConnection
            }

    /// Release all passive connections and stop producing new one.
    member _.Stop() =
        stopping.Cancel()

        for pq in connections.Values do
            Exception.Ignore pq.Timer.Dispose
            pq.Queue |> Seq.iter((_.Close) >> Exception.Ignore)

    interface IDisposable with
        /// Release all passive connections and stop producing new one.
        override pool.Dispose() = pool.Stop()
