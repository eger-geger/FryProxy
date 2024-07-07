module FryProxy.Tests.IO.AsyncTimeoutTests

open System
open System.IO
open System.Threading.Tasks

open FryProxy.IO
open FryProxy.Extension

open FryProxy.Tests.Constraints

open NUnit.Framework

[<Literal>]
let timeoutMS = 500

let timeout = TimeSpan.FromMilliseconds(timeoutMS)

type SlowStream(buff: byte array) =
    inherit MemoryStream(buff)
    override _.CanTimeout = true
    override _.ReadTimeout = timeoutMS
    override _.WriteTimeout = timeoutMS

    override _.ReadAsync(_, ct) =
        ValueTask.FromTask
        <| task {
            while true do
                ct.ThrowIfCancellationRequested()
                do! Task.Delay(100)

            return 0
        }

    override _.WriteAsync(_, ct) =
        ValueTask
        <| task {
            while true do
                ct.ThrowIfCancellationRequested()
                do! Task.Delay(100)

            return ()
        }

    override _.WriteAsync(_, _, _, ct) =
        task {
            while true do
                ct.ThrowIfCancellationRequested()
                do! Task.Delay(100)

            return ()
        }



[<Test>]
let testAsyncReadTimeout () =
    task {
        let buff = Memory(Array.zeroCreate 10)
        use ss = new SlowStream(Array.zeroCreate 10)
        use ts = new AsyncTimeoutDecorator(ss)

        ts.ReadAsync(buff) |> shouldThrowAsync<ReadTimeoutException>.From
    }

[<Test>]
let testAsyncWriteTimeout () =
    task {
        let buff = ReadOnlyMemory(Array.zeroCreate 10)
        use ss = new SlowStream(Array.zeroCreate 10)
        use ts = new AsyncTimeoutDecorator(ss)

        ts.WriteAsync(buff) |> shouldThrowAsync<WriteTimeoutException>.From
    }

[<Test>]
let testCopyFromReadTimeout () =
    task {
        use ss = new SlowStream(Array.zeroCreate 10)
        use ts = new AsyncTimeoutDecorator(ss)
        use dst = new MemoryStream(Array.zeroCreate 20)

        ts.CopyToAsync(dst) |> shouldThrowAsync<ReadTimeoutException>.From
    }

[<Test>]
let testCopyToWriteTimeout () =
    task {
        use ss = new SlowStream(Array.zeroCreate 10)
        use ts = new AsyncTimeoutDecorator(ss)
        use src = new MemoryStream(Array.zeroCreate 10)

        src.CopyToAsync(ts) |> shouldThrowAsync<WriteTimeoutException>.From
    }
