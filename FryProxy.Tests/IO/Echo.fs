module FryProxy.Tests.IO.Echo

open System
open System.Buffers
open System.Collections.Generic
open System.IO
open System.Net
open System.Net.Sockets
open System.Threading
open System.Threading.Tasks
open FsCheck
open FsCheck.FSharp
open FsCheck.NUnit


let writeSize (stream: Stream) size =
    stream.WriteAsync([| byte(size >>> 8); byte(size) |], 0, 2)

let readSize (stream: Stream) =
    task {
        let buff = [| 0uy; 0uy |]

        match! stream.ReadAsync(buff, 0, 2) with
        | 2 -> return int buff[1] + int buff[0] <<< 8
        | n -> return invalidOp $"read {n} bytes, but expected 2"
    }

let echo buff (stream: Stream) =
    task {
        let mutable sent = 0
        let! N = stream.ReadAsync(buff)
        do! writeSize stream N

        while sent < N do
            let! n = readSize stream
            do! stream.WriteAsync(buff.Slice(sent, n))
            do sent <- sent + n
    }

/// TCP server repeating a messages back to the clients, but in smaller chunks.
/// Upon establishing a connection it reads up to 2KB message from the client and replies with a message length
/// as 2 bytes number(integer). Then it will repeatedly read a single byte indicating the size of the next chunk and
/// reply with a subsequent chunk of the original message until whole of it was sent back or client closes a connection.
type Server() =
    let mutable connCount = 0
    let listener = new TcpListener(IPAddress.Loopback, 0)
    let shutdownTokenSource = new CancellationTokenSource()

    let updateCounter handler a =
        task {
            do Interlocked.Increment(&connCount) |> ignore

            try
                return! handler a
            finally
                do Interlocked.Decrement(&connCount) |> ignore
        }

    let waitConn cnt until =
        ValueTask
        <| task {
            while connCount <> cnt && DateTime.Now < until do
                do! Task.Delay(25)

            if connCount <> cnt then
                do!
                    $"Wait failed: expected [{cnt}], but there are [{connCount}] connections"
                    |> NUnit.Framework.TestContext.Error.WriteLineAsync
        }

    let echo (client: TcpClient) =
        task {
            use client = client
            use stream = client.GetStream()
            use memLease = MemoryPool.Shared.Rent(0x800)

            while client.Connected do
                do! echo memLease.Memory stream
        }

    do listener.Start()

    do
        ignore
        <| task {
            while listener.Server.IsBound && not shutdownTokenSource.IsCancellationRequested do
                let! client = listener.AcceptTcpClientAsync(shutdownTokenSource.Token)
                updateCounter echo client |> ignore
        }

    member _.Endpoint: IPEndPoint = downcast listener.LocalEndpoint

    member _.ConnectionCount: int inref = &connCount

    member _.WaitConnectionCountAsync (expected: int) (timeout: TimeSpan) =
        { new IAsyncDisposable with
            override _.DisposeAsync() =
                DateTime.Now + timeout |> waitConn expected }


    member _.Stop() =
        shutdownTokenSource.Cancel()
        shutdownTokenSource.Dispose()
        listener.Stop()

    interface IDisposable with
        member this.Dispose() = this.Stop()

module EchoTests =

    let boundBySum limit values =
        let generator (values, limit) =
            let head = Seq.head values
            let tail = Seq.tail values

            if limit = 0 then None
            else if head > limit then Some(limit, (tail, 0))
            else Some(head, (tail, limit - head))

        Seq.unfold generator (values, int limit)

    [<Property>]
    let sequenceCanBeBoundByAccumulatedSum (l: uint) =
        let chunks = Seq.initInfinite((*) 5) |> Seq.tail |> boundBySum l |> Seq.toList

        let isBound =
            chunks
            |> List.sum
            |> (=)(int l)
            |> Prop.label "values are bound by cumulative sum"

        let isGrowing =
            lazy
                chunks
                |> List.rev
                |> List.tail
                |> List.pairwise
                |> List.forall(fun (prev, next) -> prev > next)
                |> Prop.label "values are growing"

        isBound .&. (chunks.Length > 2 ==> isGrowing) |> Prop.trivial(l = 0u)


    [<Property>]
    let sizeIsSerializableAndDeserializable (n: int) =
        Prop.ofTestable
        <| task {
            use stream = new MemoryStream()

            do! writeSize stream n
            do stream.Seek(0, SeekOrigin.Begin) |> ignore
            let! n' = readSize stream

            return n' = n |> Prop.label $"expected {n} but read {n'}"
        }

    [<Property>]
    let echoOriginalMessageBackInChunks (arr: byte NonEmptyArray) =
        let msg = arr.Get

        let chunks =
            Seq.initInfinite(fun i -> 1 + i * i)
            |> boundBySum(uint msg.Length)
            |> Seq.scan (fun (off, s) s' -> (off + s, s')) (0, 0)
            |> Seq.tail
            |> Seq.toList

        Prop.ofTestable
        <| task {
            let echoChunks = List()
            use buffLease = MemoryPool.Shared.Rent(msg.Length)

            use server = new Server()
            use client = new TcpClient()

            do! client.ConnectAsync(server.Endpoint)
            use stream = client.GetStream()

            do! stream.WriteAsync msg
            let! echoSize = readSize stream
            let respBuff = buffLease.Memory

            for offset, size in chunks do
                do! writeSize stream size
                let! chunkSize = stream.ReadAsync(respBuff.Slice(offset))
                do chunkSize |> echoChunks.Add

            return
                echoSize = msg.Length |> Prop.label "echoes message size back"
                .&. (msg = respBuff.ToArray() |> Prop.label "echoes message back")
                .&. (Seq.forall2 (=) echoChunks (chunks |> List.map snd)
                     |> Prop.label "echos chunks of the requested size")
        }
