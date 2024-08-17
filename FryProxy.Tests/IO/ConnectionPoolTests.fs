module FryProxy.Tests.IO.ConnectionPoolTests

open System
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
        use _ =
            server.WaitConnectionCount (server.ConnectionCount - 1) (TimeSpan.FromSeconds(1000))

        (popConn i).Close()

    member _.Release i = (popConn i).Dispose()

    member _.Stop() = server.Stop()

    override _.ToString() = server.ToString()

[<Struct>]
type connection =
    { Active: bool; Pending: byte ReadOnlyMemory; Chunks: byte ReadOnlyMemory list }

let isReadable { Active = active; Pending = pending } = active && (not pending.IsEmpty)

type session = connection list

type ConnectionsModel = { Client: int; Server: int }

let numberOfConnectionMatches (cs: ClientServerSession, m: session) =
    cs.ServerCounter = m.Length
    |> Prop.label $"number of open connections ({cs.ServerCounter}) should be {m.Length}"
    |> Prop.trivial(m.Length = 0)

let openConn payload =
    { new Operation<ClientServerSession, session>() with
        member _.Check(cs, m) =
            do cs.Open(payload).Wait()
            numberOfConnectionMatches(cs, m)

        override _.Pre _ = not payload.IsEmpty

        member _.Run sess =
            let conn = { Active = true; Pending = payload; Chunks = [] }

            let rec upsert xs ys =
                match xs with
                | { Active = false } :: tail -> List.rev tail @ conn :: ys
                | active :: tail -> active :: ys |> upsert tail
                | [] -> conn :: ys

            upsert sess [] |> List.rev

        override _.ToString() = "open a connection" }

let closeConn i =
    { new Operation<ClientServerSession, session>() with
        override _.Check(cs, m) =
            do cs.Close(i)
            numberOfConnectionMatches(cs, m)

        override _.Pre sess =
            match List.tryItem i sess with
            | Some conn -> conn.Active
            | _ -> false

        override _.Run sess = sess |> List.removeAt i

        override _.ToString() = $"close {i}# connection" }

let readConn i n =
    { new Operation<ClientServerSession, session>() with
        override _.Check(cs, sess) =
            let chunk = cs.Read(i, n).Result

            let contentEqual = chunk.Span.SequenceEqual sess[i].Chunks.Head.Span

            assert contentEqual

            numberOfConnectionMatches(cs, sess)
            .&. (contentEqual |> Prop.label "payload chunk is read")

        override _.Pre sess =
            match List.tryItem i sess with
            | Some conn -> isReadable conn
            | _ -> false

        override _.Run sess =
            match List.splitAt i sess with
            | prefix, conn :: suffix when isReadable conn ->
                let upd =
                    { conn with
                        Pending = conn.Pending.Slice(n)
                        Chunks = conn.Pending.Slice(0, n) :: conn.Chunks }

                prefix @ upd :: suffix
            | _ -> invalidOp $"cannot read from {sess[i]}"


        override _.ToString() = $"read {n}b from #{i} connection" }

let machine =
    { new Machine<ClientServerSession, session>(20) with
        override _.Next sess =
            gen {
                let closeOps = [ 0 .. sess.Length - 1 ] |> List.map closeConn

                let! readOps =
                    sess
                    |> List.indexed
                    |> List.filter(snd >> isReadable)
                    |> List.map(fun (i, conn) -> (1, conn.Pending.Length) |> Gen.choose |> Gen.map(readConn i))
                    |> Gen.sequenceToList

                let! payloadSize = Gen.growingElements [ 0xf; 0xff; 0x200; 0x800 ]
                let! payload = Gen.choose(0, 0xff) |> Gen.arrayOfLength payloadSize

                let openOp = payload |> Array.map byte |> ReadOnlyMemory |> openConn

                return! [ openOp ] @ closeOps @ readOps @ readOps |> Gen.elements
            }

        override _.Setup =
            let pool = ConnectionPool(BufferSize, TimeSpan.FromSeconds(1))

            let setup =
                { new Setup<ClientServerSession, session>() with
                    override _.Actual() = ClientServerSession(pool)
                    override _.Model() = List.empty }

            Gen.constant setup |> Arb.fromGen

        override _.TearDown =
            { new TearDown<ClientServerSession>() with
                override _.Actual cp = cp.Stop() }

    }

[<Property(Parallelism = 16)>]
let testConnectionPool () = StateMachine.toProperty machine
