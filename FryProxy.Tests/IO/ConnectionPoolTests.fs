module FryProxy.Tests.IO.ConnectionPoolTests

open System
open System.Threading.Tasks
open FryProxy.IO
open FryProxy.Tests.IO
open FsCheck.Experimental
open FsCheck.FSharp
open NUnit.Framework

[<Literal>]
let BufferSize = 0x800

type ClientServerSession(pool: ConnectionPool) =
    let server = new Echo.Server(BufferSize)

    let mutable connections = List.Empty

    let popConn i =
        match List.splitAt i connections with
        | prefix, conn :: suffix ->
            connections <- prefix @ suffix
            conn
        | _ -> invalidArg (nameof i) $"{i} out of range [0; {connections.Length}]"

    let echoSizeError expected actual =
        invalidOp $"echo size mismatch: expected {expected}, but was {actual}"

    member _.ServerCounter = server.ConnectionCount

    member _.Open(msg: _ ReadOnlyMemory) =
        task {
            let! conn = pool.ConnectAsync(server.Endpoint)
            do! conn.Buffer.Stream.WriteAsync(msg)

            match! Echo.readSize conn.Buffer.Stream with
            | n when n <> msg.Length -> echoSizeError msg.Length n
            | _ -> do connections <- connections @ [ conn ]
        }

    member _.Read(i, n) =
        task {
            let conn = connections[i]

            do! n |> Echo.writeSize conn.Buffer.Stream

            match! conn.Buffer.Fill() with
            | c when c <> n -> return echoSizeError n c
            | _ ->
                let copy = Memory(conn.Buffer.Pending.ToArray())
                conn.Buffer.Discard(n)
                return copy
        }


    member _.Close i =
        use wait = server.WaitConnectionClose(TimeSpan.FromMilliseconds(1000))

        (popConn i).Close()

    member _.Release i = (popConn i).Dispose()

    member _.Stop() = server.Stop()

    override _.ToString() = server.ToString()

    interface IDisposable with
        override this.Dispose() = this.Stop()

[<Struct>]
type ActiveConnection = { Pending: byte ReadOnlyMemory; Chunk: byte ReadOnlyMemory }

[<Struct>]
type PassiveConnection = { Since: DateTime }

[<Struct>]
type Session = { Active: ActiveConnection list; Passive: int }

let numberOfConnectionMatches op (cs: ClientServerSession, m: Session) =
    let connCount = m.Active.Length + m.Passive

    cs.ServerCounter = connCount
    |> Prop.label $"number of open connections ({cs.ServerCounter}) should be {connCount} following the {op}"
    |> Prop.trivial(connCount = 0)

let openConn payload =
    { new Operation<ClientServerSession, Session>() with
        member op.Check(cs, m) =
            do cs.Open(payload).Wait()
            numberOfConnectionMatches op (cs, m)

        override _.Pre _ = not payload.IsEmpty

        member _.Run sess =
            let conn = { Pending = payload; Chunk = ReadOnlyMemory.Empty }

            { sess with
                Active = sess.Active @ [ conn ]
                Passive = sess.Passive - 1 |> max 0 }

        override _.ToString() = "open a connection" }

let closeConn i =
    { new Operation<ClientServerSession, Session>() with
        override op.Check(cs, m) =
            do cs.Close(i)
            numberOfConnectionMatches op (cs, m)

        override _.Pre sess = sess.Active.Length > i

        override _.Run sess =
            { sess with Active = sess.Active |> List.removeAt i }

        override _.ToString() = $"close {i}# connection" }

let readConn i n =
    { new Operation<ClientServerSession, Session>() with
        override op.Check(cs, sess) =
            let chunk = cs.Read(i, n).Result

            numberOfConnectionMatches op (cs, sess)
            .&. (chunk.Span.SequenceEqual sess.Active[i].Chunk.Span
                 |> Prop.label "payload chunk is read")

        override _.Pre sess =
            n > 0
            && match List.tryItem i sess.Active with
               | Some conn -> conn.Pending.Length >= n
               | _ -> false

        override _.Run sess =
            let conn = sess.Active[i]

            { sess with
                Active =
                    List.updateAt i
                    <| { conn with Pending = conn.Pending.Slice(n); Chunk = conn.Pending.Slice(0, n) }
                    <| sess.Active }

        override _.ToString() = $"read {n}b from #{i} connection" }

let releaseConn i =
    { new Operation<ClientServerSession, Session>() with
        override op.Check(cs, sess) =
            do cs.Release i
            numberOfConnectionMatches op (cs, sess)

        override _.Pre sess =
            match List.tryItem i sess.Active with
            | Some conn -> conn.Pending.IsEmpty
            | _ -> false

        override _.Run sess =
            { sess with Active = List.removeAt i sess.Active; Passive = sess.Passive + 1 }

        override _.ToString() = $"release #{i} connection" }

let machine =
    let passiveTimeout = TimeSpan.FromSeconds(2)

    let readGen sizes i (conn: ActiveConnection) =
        sizes |> List.takeWhile((>=) conn.Pending.Length) |> List.append
        <| [ conn.Pending.Length ]
        |> List.distinct
        |> Gen.growingElements
        |> Gen.map(readConn i)

    { new Machine<ClientServerSession, Session>(20) with
        override _.Next sess =
            gen {
                let closeOps = [ 0 .. sess.Active.Length - 1 ] |> List.map closeConn

                let releaseOps =
                    sess.Active
                    |> List.indexed
                    |> List.filter(snd >> (_.Pending.IsEmpty))
                    |> List.map(fst >> releaseConn)

                let! reads =
                    sess.Active
                    |> List.indexed
                    |> List.filter(snd >> (_.Pending.IsEmpty) >> not)
                    |> List.map((<||)(readGen [ 0x20; 0x80; 0x180; 0x360 ]))
                    |> Gen.sequenceToList

                let! payloadSize = Gen.growingElements [ 0x10; 0x100; 0x200; 0x400 ]
                let! payload = Gen.choose(0, 0xff) |> Gen.arrayOfLength payloadSize
                let openOp = payload |> Array.map byte |> ReadOnlyMemory |> openConn

                return! [ openOp ] @ closeOps @ reads @ reads @ releaseOps |> Gen.elements
            }

        override _.Setup =
            let pool = ConnectionPool(BufferSize, passiveTimeout)

            let setup =
                { new Setup<ClientServerSession, Session>() with
                    override _.Actual() = new ClientServerSession(pool)

                    override _.Model() = { Active = List.empty; Passive = 0 } }


            Gen.constant setup |> Arb.fromGen

        override _.TearDown =
            { new TearDown<ClientServerSession>() with
                override _.Actual cp = cp.Stop() }

    }

[<FsCheck.NUnit.Property(Parallelism = 12)>]
let ``connection count remain consistent`` () = StateMachine.toProperty machine

[<Test>]
let ``connection expires after a timeout`` () =
    let timeout = TimeSpan.FromSeconds(0.5)

    task {
        let payload = ReadOnlyMemory(Array.zeroCreate 64)
        let pool = ConnectionPool(BufferSize, timeout)
        use session = new ClientServerSession(pool)

        do! session.Open(payload)
        do! session.Open(payload)
        do! session.Open(payload)

        do session.Close(0)
        do session.Release(0)
        do! Task.Delay(250)
        do session.Release(0)

        let delayedCount () = session.ServerCounter

        Assert.That(delayedCount(), Is.EqualTo(2))
        Assert.That<int>(delayedCount, Is.EqualTo(1).After(1000, 50))
        Assert.That<int>(delayedCount, Is.EqualTo(0).After(1000, 50))
    }
