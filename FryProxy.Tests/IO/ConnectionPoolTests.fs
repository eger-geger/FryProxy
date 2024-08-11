module FryProxy.Tests.IO.ConnectionPoolTests

open System
open System.Net
open System.Net.Sockets
open System.Threading
open FryProxy.IO
open FsCheck.Experimental
open FsCheck.FSharp
open FsCheck.NUnit

type DummyServer() =
    let mutable counter = 0
    let listener = new TcpListener(IPAddress.Loopback, 0)
    let cancelOnClose = new CancellationTokenSource()

    let updateCounter handler a =
        task {
            Interlocked.Increment(&counter) |> ignore

            try
                return! handler a
            finally
                Interlocked.Decrement(&counter) |> ignore
        }

    let echo (client: TcpClient) =
        let buffer: byte[] = Array.zeroCreate 0xFF

        task {
            use stream = client.GetStream()

            while client.Connected do
                do! stream.WriteAsync(buffer)

                if client.Available > 0 then
                    let! _ = stream.ReadAsync(buffer)
                    ()
        }

    do listener.Start()

    do
        ignore
        <| task {
            while listener.Server.IsBound && not cancelOnClose.IsCancellationRequested do
                let! client = listener.AcceptTcpClientAsync(cancelOnClose.Token)
                updateCounter echo client |> ignore
        }

    member _.Endpoint: IPEndPoint = downcast listener.LocalEndpoint

    member _.Counter: int inref = &counter

    member _.Stop() =
        cancelOnClose.Cancel()
        cancelOnClose.Dispose()
        listener.Stop()

type ConnectedPool(pool: ConnectionPool) =
    let server = DummyServer()

    let mutable connections = List.Empty

    let popConn i =
        let suffix, prefix = List.splitAt i connections
        connections <- suffix @ prefix.Tail
        prefix.Head

    member _.ServerCounter = server.Counter

    member _.Open() =
        let conn = pool.ConnectAsync(server.Endpoint).Result
        conn.Buffer.Fill().Result |> ignore
        connections <- conn :: connections

    member _.Read(i, n) =
        let conn = popConn(i)
        do conn.Buffer.Fill().Result |> ignore
        do conn.Buffer.Discard(n)
        do conn.Dispose()

    member _.Close i =
        (popConn i).Close()
        Thread.Sleep(50)

    member _.Release i = (popConn i).Dispose()

    member _.Stop() = server.Stop()

    override _.ToString() = $"{server.Counter}@{server.Endpoint}"



[<Struct>]
type ConnectionsModel = { Client: int; Server: int }

let connectionsAreOpen (p: ConnectedPool, m: ConnectionsModel) =
    p.ServerCounter = m.Server
    |> Prop.label $"number of open server connections ({p.ServerCounter}) should  match the model ({m.Server})"
    |> Prop.trivial(m.Server = 0)

let openConn =
    { new Operation<ConnectedPool, ConnectionsModel>() with
        member _.Check(cs, m) =
            do cs.Open()
            connectionsAreOpen(cs, m)

        member _.Run m =
            if m.Client < m.Server then
                { m with Client = m.Client + 1 }
            else
                { m with Client = m.Client + 1; Server = m.Server + 1 }

        override _.ToString() = "open a connection" }

let closeConn i =
    { new Operation<ConnectedPool, ConnectionsModel>() with
        override _.Check(cs, m) =
            do cs.Close(i)
            connectionsAreOpen(cs, m)

        override _.Pre m = i < m.Client

        override _.Run m =
            { m with Client = m.Client - 1; Server = m.Server - 1 }

        override _.ToString() = $"close {i}# connection" }

let readConn i n =
    { new Operation<ConnectedPool, ConnectionsModel>() with
        override _.Check(cs, m) =
            do cs.Read(i, n)
            connectionsAreOpen(cs, m)

        override _.Pre m = i < m.Client

        override _.Run m = { m with Client = m.Client - 1 }

        override _.ToString() = $"read {n}bytes from {i}# connection" }

let machine =
    let pool = ConnectionPool(0x80, TimeSpan.FromSeconds(1))

    { new Machine<ConnectedPool, ConnectionsModel>(20) with
        override _.Next m =
            gen {
                let closeOps = [ 0 .. m.Client - 1 ] |> List.map closeConn
                let readOps = [ 0 .. m.Client - 1 ] |> List.map(readConn >> (|>) 0x80)
                return! [ openConn ] @ closeOps @ readOps @ readOps |> Gen.elements
            }

        override _.Setup =
            let setup =
                { new Setup<ConnectedPool, ConnectionsModel>() with
                    override _.Actual() = ConnectedPool(pool)
                    override _.Model() = { Client = 0; Server = 0 } }

            Gen.constant setup |> Arb.fromGen

        override _.TearDown =
            { new TearDown<ConnectedPool>() with
                override _.Actual cp = cp.Stop() }

    }

[<Property(Parallelism = 4)>]
let testConnectionPool () = StateMachine.toProperty machine
