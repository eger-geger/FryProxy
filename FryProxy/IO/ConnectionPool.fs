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

module Connection =

    /// Backed by null stream and zero-length buffer.
    let Empty =
        { new IConnection with
            member _.Buffer = ReadBuffer(Memory.Empty, Stream.Null)
            member _.Close() = ()
            member _.Dispose() = () }



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

// Initializes a fresh pooled connection.
type ConnectionInit = NetworkStream -> Stream ValueTask

/// Pool of outgoing TCP connections paired with a read buffer. Releases passive connection after a timeout.
type ConnectionPool(bufferSize: int, timeout: TimeSpan) =
    static let defaultInit: ConnectionInit = id >> ValueTask.FromResult<Stream>
    let stopping = new CancellationTokenSource()
    let connections = ConcurrentDictionary<EndPoint * _ voption, PoolQueue>()

    let connect (init: ConnectionInit) (ip: EndPoint) =
        task {
            let memLease = MemoryPool.Shared.Rent(bufferSize)

            try
                let socket = new Socket(SocketType.Stream, ProtocolType.Tcp)
                socket.ReceiveBufferSize <- bufferSize

                do! socket.ConnectAsync(ip, stopping.Token)
                let! initialized = init(new NetworkStream(socket, true))
                
                return PooledConnection(memLease, socket, initialized)
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

    let expire (state: obj) =
        let key = state :?> EndPoint * _ voption
        let mutable pool = defaultof<_>
        let mutable conn = defaultof<_>

        if connections.TryGetValue(key, &pool) && pool <> defaultof<_> then
            if pool.Queue.TryDequeue(&conn) then
                do conn.Close()

            if pool.Queue.IsEmpty && connections.TryRemove(key, &pool) then
                do clear pool
            else
                do reschedule pool

    let enqueue key (conn: PooledConnection) closed =
        if closed then
            do ()
        else if stopping.IsCancellationRequested || timeout = TimeSpan.Zero then
            do conn.Close()
        else
            let inline add _ = PoolQueue.New(conn, key, expire)
            let inline upd _ pool = let _ = pool.Queue.Enqueue conn in pool
            do connections.AddOrUpdate(key, add, upd) |> reschedule

    let dequeue key =
        let mutable pool = defaultof<_>
        let mutable conn = defaultof<_>

        if
            timeout > TimeSpan.Zero
            && connections.TryGetValue(key, &pool)
            && pool.Queue.TryDequeue(&conn)
        then
            do reschedule pool
            ValueSome(conn.Lease().OnDispose(enqueue key conn))
        else
            ValueNone

    let establish init (point, props) =
        task {
            let! conn = connect init point
            return conn.Lease().OnDispose(enqueue (point, props) conn) :> IConnection
        }

    new(bufferSize) = new ConnectionPool(bufferSize, TimeSpan.FromSeconds(30))

    /// Acquire a temporary ownership over new or pooled connection, which is returned back to the pool when lease
    /// ends unless it was closed.
    member pool.ConnectAsync(ep: EndPoint) : IConnection ValueTask =
        pool.ConnectAsync(ep, ValueNone, defaultInit)

    member _.ConnectAsync(ep: EndPoint, attr: _ voption, init: ConnectionInit) : IConnection ValueTask =
        let key = ep, attr
        let pooled = lazy dequeue key

        if stopping.IsCancellationRequested then
            ValueTask.FromCanceled<_>(stopping.Token)
        else if pooled.Value.IsSome then
            ValueTask.FromResult(pooled.Force().Value)
        else
            ValueTask.FromTask(establish init key)


    /// Release all passive connections and stop producing new one.
    member _.Stop() =
        stopping.Cancel()

        for pq in connections.Values do
            Exception.Ignore pq.Timer.Dispose
            pq.Queue |> Seq.iter((_.Close) >> Exception.Ignore)
            do pq.Queue.Clear()

        do connections.Clear()


    interface IDisposable with
        /// Release all passive connections and stop producing new one.
        override pool.Dispose() = pool.Stop()
