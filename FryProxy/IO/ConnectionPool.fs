namespace FryProxy.IO

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
type internal ScopedConnection(rb: ReadBuffer, clear: bool -> unit) =
    let mutable closed = false

    /// Execute a callback when lease ends or connection closes.
    /// Callback accepts a flag indicating whether connection will be closed.
    member _.OnDispose fn =
        let inline combine close =
            do clear close
            do fn close

        new ScopedConnection(rb, combine)


    interface IConnection with
        member _.Buffer = rb

        member _.Close() =
            closed <- true
            clear(true)

        member _.Dispose() =
            if not closed then
                clear(closed)

type internal PooledConnection(ownedMem: byte IMemoryOwner, socket: Socket, stream: Stream) =
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
            new ScopedConnection(rb.Value, clearLease this)

    /// Close underlying socket and release other connection resources.
    member _.Close() =
        do Exception.Ignore stream.Dispose
        do Exception.Ignore socket.Dispose
        do Exception.Ignore ownedMem.Dispose

[<Struct>]

type internal PoolQueue =
    { Timer: Timer
      Queue: PooledConnection ConcurrentQueue }

    static member New(conn, state, handler) =
        { Queue = ConcurrentQueue([ conn ])
          Timer = new Timer(handler, state, Timeout.Infinite, Timeout.Infinite) }

    static member val Zero = { Timer = null; Queue = null }


type ConnectionPool(bufferSize: int, idleTimeout: TimeSpan) =

    let connections = ConcurrentDictionary<IPEndPoint, PoolQueue>()

    let connect (ip: IPEndPoint) =
        task {
            let memLease = MemoryPool.Shared.Rent(bufferSize)

            try
                let socket = new Socket(SocketType.Stream, ProtocolType.Tcp)
                socket.ReceiveBufferSize <- bufferSize

                do! socket.ConnectAsync(ip)

                return PooledConnection(memLease, socket, new NetworkStream(socket, true))
            with ex ->
                memLease.Dispose()
                return ex.Rethrow()
        }

    let clear { Queue = q; Timer = t } =
        Exception.Ignore t.Dispose
        q |> Seq.iter(_.Close())

    let dequeue (state: obj) =
        let ip = state :?> IPEndPoint
        let mutable pool = PoolQueue.Zero
        let mutable conn = Unchecked.defaultof<_>

        if connections.TryGetValue(ip, &pool) then
            if pool.Queue.TryDequeue(&conn) then
                do conn.Close()

            if pool.Queue.IsEmpty && connections.TryRemove(ip, &pool) then
                do clear pool

    let reschedule idleTimeout { Queue = q; Timer = t } =
        let mutable next = Unchecked.defaultof<_>

        let delay =
            if q.TryPeek(&next) then
                idleTimeout - next.IdleDuration
            else
                idleTimeout

        t.Change(max delay TimeSpan.Zero, Timeout.InfiniteTimeSpan) |> ignore

    let enqueue ip conn close =
        if not close then
            let inline add _ = PoolQueue.New(conn, ip, dequeue)
            let inline upd _ ({ Queue = q } as pool) = let _ = q.Enqueue conn in pool
            connections.AddOrUpdate(ip, add, upd) |> reschedule idleTimeout

    /// Acquire a temporary ownership over new or pooled connection, which is returned back to the pool when lease
    /// ends unless it was closed.
    member _.ConnectAsync(ip: IPEndPoint) : IConnection ValueTask =
        let mutable pool = PoolQueue.Zero
        let mutable head = Unchecked.defaultof<_>

        if connections.TryGetValue(ip, &pool) && pool.Queue.TryDequeue(&head) then
            do reschedule idleTimeout pool
            ValueTask.FromResult(head.Lease().OnDispose(enqueue ip head))
        else
            ValueTask.FromTask
            <| task {
                let! conn = connect ip

                return conn.Lease().OnDispose(enqueue ip conn) :> IConnection
            }
