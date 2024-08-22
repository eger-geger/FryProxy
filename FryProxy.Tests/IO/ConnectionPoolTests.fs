module FryProxy.Tests.IO.ConnectionPoolTests

open System
open System.Threading
open FryProxy.IO
open FryProxy.Tests.IO
open FsCheck.Experimental
open FsCheck.FSharp
open FsCheck.NUnit

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

[<Struct>]
type ActiveConnection = { Pending: byte ReadOnlyMemory; Chunk: byte ReadOnlyMemory }

[<Struct>]
type PassiveConnection = { Since: DateTime }

[<Struct>]
type Session = { Active: ActiveConnection list; Passive: PassiveConnection list }

let numberOfConnectionMatches (cs: ClientServerSession, m: Session) =
    let connCount = m.Active.Length + m.Passive.Length

    cs.ServerCounter = connCount
    |> Prop.label $"number of open connections ({cs.ServerCounter}) should be {connCount}"
    |> Prop.trivial(connCount = 0)

let openConn payload =
    { new Operation<ClientServerSession, Session>() with
        member _.Check(cs, m) =
            do cs.Open(payload).Wait()
            numberOfConnectionMatches(cs, m)

        override _.Pre _ = not payload.IsEmpty

        member _.Run sess =
            let conn = { Pending = payload; Chunk = ReadOnlyMemory.Empty }
            let sess' = { sess with Active = sess.Active @ [ conn ] }

            match sess'.Passive with
            | _ :: tail -> { sess' with Passive = tail }
            | _ -> sess'


        override _.ToString() = "open a connection" }

let closeConn i =
    { new Operation<ClientServerSession, Session>() with
        override _.Check(cs, m) =
            do cs.Close(i)
            numberOfConnectionMatches(cs, m)

        override _.Pre sess = sess.Active.Length > i

        override _.Run sess =
            { sess with Active = sess.Active |> List.removeAt i }

        override _.ToString() = $"close {i}# connection" }

let readConn i n =
    { new Operation<ClientServerSession, Session>() with
        override _.Check(cs, sess) =
            let chunk = cs.Read(i, n).Result

            numberOfConnectionMatches(cs, sess)
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
        override _.Check(cs, sess) =
            do cs.Release i
            numberOfConnectionMatches(cs, sess)

        override _.Pre sess =
            match List.tryItem i sess.Active with
            | Some conn -> conn.Pending.IsEmpty
            | _ -> false

        override _.Run sess =
            { sess with
                Active = List.removeAt i sess.Active
                Passive = sess.Passive @ [ { Since = DateTime.Now } ] }

        override _.ToString() = $"release #{i} connection" }

let timeoutConn (lifetime: TimeSpan) (until: DateTime) =
    { new Operation<ClientServerSession, Session>() with

        override _.Check(cs, sess) =
            match until - DateTime.Now with
            | t when t > TimeSpan.Zero -> Thread.Sleep(t)
            | _ -> ()

            numberOfConnectionMatches(cs, sess)

        override _.Pre sess =
            sess.Passive.Length > 0 && DateTime.Now < until

        override _.Run sess =
            let limit = until - lifetime
            { sess with Passive = sess.Passive |> List.filter((_.Since) >> (<) limit) }

        override _.ToString() = $"wait until {until}"

    }

let waitPoints (margin: TimeSpan) connections =
    let distribute (points: DateTime list) =
        points
        |> List.pairwise
        |> List.map(fun (a, b) ->
            match (b - a) / 2. with
            | d when d < margin -> [ (a + d) ]
            | _ -> List.empty)
        |> List.concat

    match connections with
    | [] -> List.empty
    | [ head ] ->
        [ DateTime.Now; head.Since ] |> distribute |> List.append
        <| [ head.Since + margin ]
    | items ->
        items
        |> List.map(_.Since)
        |> List.append [ DateTime.Now ]
        |> distribute
        |> List.append
        <| [ (List.last items).Since + margin ]


let machine =
    let passiveLifetime = TimeSpan.FromSeconds(1)
    let delayTolerance = passiveLifetime / 2.

    { new Machine<ClientServerSession, Session>(20) with
        override _.Next sess =
            gen {
                let closeOps = [ 0 .. sess.Active.Length - 1 ] |> List.map closeConn

                let releaseOps =
                    sess.Active
                    |> List.indexed
                    |> List.filter(snd >> (_.Pending.IsEmpty))
                    |> List.map(fst >> releaseConn)

                let! readOps =
                    sess.Active
                    |> List.indexed
                    |> List.filter(fun (_, conn) -> conn.Pending.Length > 0)
                    |> List.map(fun (i, conn) -> (1, conn.Pending.Length) |> Gen.choose |> Gen.map(readConn i))
                    |> Gen.sequenceToList

                let! advanceOps =
                    sess.Passive
                    |> waitPoints delayTolerance
                    |> Gen.elements
                    |> Gen.map(timeoutConn passiveLifetime)
                    |> Gen.listOfLength sess.Passive.Length

                let! payloadSize = Gen.growingElements [ 0x10; 0x100; 0x200; 0x400 ]
                let! payload = Gen.choose(0, 0xff) |> Gen.arrayOfLength payloadSize
                let openOp = payload |> Array.map byte |> ReadOnlyMemory |> openConn

                return!
                    [ openOp ] @ closeOps @ readOps @ readOps @ releaseOps @ advanceOps
                    |> Gen.elements
            }

        override _.Setup =
            let pool = ConnectionPool(BufferSize, passiveLifetime)

            let setup =
                { new Setup<ClientServerSession, Session>() with
                    override _.Actual() = ClientServerSession(pool)

                    override _.Model() =
                        { Active = List.empty; Passive = List.empty } }


            Gen.constant setup |> Arb.fromGen

        override _.TearDown =
            { new TearDown<ClientServerSession>() with
                override _.Actual cp = cp.Stop() }

    }

[<Property(Parallelism = 12)>]
let testConnectionPool () = StateMachine.toProperty machine
