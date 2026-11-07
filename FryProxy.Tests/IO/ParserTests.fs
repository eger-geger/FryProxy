module FryProxy.Tests.IO.ParserTests

open System
open System.IO
open FryProxy.IO
open FryProxy.IO.BufferedParser
open FryProxy.Tests.Constraints
open FsUnit
open NUnit.Framework

[<Test>]
let ``parser discard skips bytes`` () =
    task {
        let source = [| 1uy; 2uy; 3uy; 4uy; 5uy |]
        let stream = new MemoryStream(source, false)
        let rb = ReadBuffer(Memory(Array.zeroCreate 16), stream)
        let! _ = rb.Fill()
        let! () = Parser.discard 2UL |> Parser.run rb
        rb.Pending.ToArray() |> should equal [| 3uy; 4uy; 5uy |]
    }

[<Test>]
let ``parser discard zero bytes is no-op`` () =
    task {
        let source = [| 1uy; 2uy; 3uy |]
        let stream = new MemoryStream(source, false)
        let rb = ReadBuffer(Memory(Array.zeroCreate 16), stream)
        let! _ = rb.Fill()
        let! () = Parser.discard 0UL |> Parser.run rb
        rb.Pending.ToArray() |> should equal source
    }

[<Test>]
let ``parser discard fails on uncommitted offset`` () =
    task {
        let source = [| 1uy; 2uy; 3uy; 4uy |]
        let stream = new MemoryStream(source, false)
        let rb = ReadBuffer(Memory(Array.zeroCreate 16), stream)
        let! _ = rb.Fill()
        // pickByte consumes 1 byte but doesn't commit, leaving offset > 0
        let result = Parser.bind (fun _ -> Parser.discard 2UL) Parser.pickByte |> Parser.run rb
        result |> shouldThrowAsync<ParseError>.From
    }

[<Test>]
let ``parser discard after commit works`` () =
    task {
        let source = [| 1uy; 2uy; 3uy; 4uy; 5uy |]
        let stream = new MemoryStream(source, false)
        let rb = ReadBuffer(Memory(Array.zeroCreate 16), stream)
        let! _ = rb.Fill()
        // pickByte + commit clears offset, then discard should work
        let! one = Parser.pickByte |> Parser.commit |> Parser.run rb
        one |> should equal 1uy
        let! () = Parser.discard 2UL |> Parser.run rb
        rb.Pending.ToArray() |> should equal [| 4uy; 5uy |]
    }

[<Test>]
let ``parser discard reads from stream`` () =
    task {
        let source = [| 1uy; 2uy; 3uy; 4uy; 5uy |]
        let stream = new MemoryStream(source, false)
        // Small buffer to force stream reads
        let rb = ReadBuffer(Memory(Array.zeroCreate 2), stream)
        let! _ = rb.Fill()
        // Discard 4 bytes: 2 from buffer + 2 from stream
        let! () = Parser.discard 4UL |> Parser.run rb
        // After discard, pending may be empty; fill to see remaining
        let! _ = rb.Fill()
        rb.Pending.ToArray() |> should equal [| 5uy |]
    }
