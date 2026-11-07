module FryProxy.Tests.IO.ReadBufferDiscardTests

open System
open System.IO
open FryProxy.IO
open FsUnit
open NUnit.Framework

[<Test>]
let ``discard reads zero bytes`` () =
    task {
        let source = [| 1uy; 2uy; 3uy; 4uy |]
        let stream = new MemoryStream(source, false)
        let rb = ReadBuffer(Memory(Array.zeroCreate 16), stream)
        let! _ = rb.Fill()
        let! discarded = ReadBuffer.discard rb 0UL
        discarded |> should equal 0UL
        rb.Pending.ToArray() |> should equal source
    }

[<Test>]
let ``discard skips bytes already in buffer`` () =
    task {
        let source = [| 1uy; 2uy; 3uy; 4uy |]
        let stream = new MemoryStream(source, false)
        let rb = ReadBuffer(Memory(Array.zeroCreate 16), stream)
        let! _ = rb.Fill()
        let! discarded = ReadBuffer.discard rb 2UL
        discarded |> should equal 2UL
        rb.Pending.ToArray() |> should equal [| 3uy; 4uy |]
    }

[<Test>]
let ``discard reads from stream when buffer exhausted`` () =
    task {
        let source = [| 1uy; 2uy; 3uy; 4uy; 5uy; 6uy |]
        let stream = new MemoryStream(source, false)
        // Small buffer so stream read is needed
        let rb = ReadBuffer(Memory(Array.zeroCreate 2), stream)
        let! _ = rb.Fill()
        // Discard 4 bytes: 2 from buffer + 2 from stream
        let! discarded = ReadBuffer.discard rb 4UL
        discarded |> should equal 4UL
        // After discard, pending may be empty; need to fill to see remaining
        let! _ = rb.Fill()
        rb.Pending.ToArray() |> should equal [| 5uy; 6uy |]
    }

[<Test>]
let ``discard returns less on EOF`` () =
    task {
        let source = [| 1uy; 2uy |]
        let stream = new MemoryStream(source, false)
        let rb = ReadBuffer(Memory(Array.zeroCreate 16), stream)
        let! _ = rb.Fill()
        // Try to discard more than available
        let! discarded = ReadBuffer.discard rb 10UL
        discarded |> should equal 2UL
    }

[<Test>]
let ``discard large value without overflow`` () =
    task {
        let source = [| 1uy; 2uy; 3uy |]
        let stream = new MemoryStream(source, false)
        let rb = ReadBuffer(Memory(Array.zeroCreate 16), stream)
        let! _ = rb.Fill()
        // Request far more than available
        let! discarded = ReadBuffer.discard rb 0xFFFFFFFFFFFFFFFFUL
        discarded |> should equal 3UL
    }
