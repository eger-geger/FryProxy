module FryProxy.Tests.Http.Frames.FrameHeaderTests

open System
open System.Buffers
open FryProxy.Http.Frames
open FsUnit
open NUnit.Framework

[<Test>]
let testEncodeDecode () =
    use memOwner = MemoryPool.Shared.Rent()

    let fh = { Length = 648u; Flags = 3uy; Type = 4uy; StreamId = 45u }

    FrameHeader.encode fh memOwner.Memory.Span |> should equal 9

    FrameHeader.decode(memOwner.Memory.Span.Slice(0, 9)) |> should equal fh
