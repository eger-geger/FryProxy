module FryProxy.Tests.IO.AsyncTimeoutTests

open System
open System.IO
open System.Threading
open System.Threading.Tasks

open FryProxy.IO
open FryProxy.Extension

open FryProxy.Tests.Constraints

open FsUnit
open Microsoft.FSharp.Collections
open NUnit.Framework

type SlowStream(buff: byte array, timeout) =
    inherit MemoryStream(buff)

    let delay (ct: CancellationToken) tsk =
        task {
            do! Task.Delay(500)
            ct.ThrowIfCancellationRequested()
            return! tsk
        }

    new() = new SlowStream(Array.zeroCreate 10, 250)

    override _.CanTimeout = true
    override _.ReadTimeout = timeout
    override _.WriteTimeout = timeout

    override _.ReadAsync(buffer, ct) =
        base.ReadAsync(buffer, ct).AsTask() |> delay ct |> ValueTask.FromTask

    override _.WriteAsync(buffer, ct) =
        base.WriteAsync(buffer, ct).AsTask().AsUnit() |> delay ct |> ValueTask

    override _.WriteAsync(buffer, offset, count, ct) =
        base.WriteAsync(buffer, offset, count, ct).AsUnit() |> delay ct :> Task


[<Test>]
let testAsyncReadTimeout () =
    task {
        let buff = Memory(Array.zeroCreate 10)
        use ts = new AsyncTimeoutDecorator(new SlowStream())

        ts.ReadAsync(buff) |> shouldThrowAsync<ReadTimeoutException>.From
    }

[<Test>]
let testAsyncWriteTimeout () =
    task {
        let buff = ReadOnlyMemory(Array.zeroCreate 10)
        use ts = new AsyncTimeoutDecorator(new SlowStream())

        ts.WriteAsync(buff) |> shouldThrowAsync<WriteTimeoutException>.From
    }

[<Test>]
let testCopyFromReadTimeout () =
    task {
        use src = new AsyncTimeoutDecorator(new SlowStream())
        use dst = new MemoryStream(Array.zeroCreate 20)

        src.CopyToAsync(dst) |> shouldThrowAsync<ReadTimeoutException>.From
    }

[<Test>]
let testCopyToWriteTimeout () =
    task {
        use dst = new AsyncTimeoutDecorator(new SlowStream())
        use src = new MemoryStream(Array.zeroCreate 10)

        src.CopyToAsync(dst) |> shouldThrowAsync<WriteTimeoutException>.From
    }

[<Test>]
let testReadWrite () =
    let squares = [| for i in 1..10 -> byte(i * i) |]
    let buf = Memory(Array.zeroCreate 10)

    task {
        let ssb = Array.zeroCreate 10
        use ss = new SlowStream(ssb, 750)
        use ts = new AsyncTimeoutDecorator(ss)

        do! ts.WriteAsync(ReadOnlyMemory(squares))
        ssb |> should equal squares
        
        ts.Seek(0, SeekOrigin.Begin) |> ignore
        
        let! n = ts.ReadAsync(buf)
        n |> should equal 10
        buf.ToArray() |> should equal squares
    }
