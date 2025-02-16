module FryProxy.Tests.Http2.FrameHeaderTests

open System.Buffers
open FryProxy.Http2
open FsUnit
open NUnit.Framework

#nowarn 3391

[<Test>]
let testEncodeDecode () =
    use memOwner = MemoryPool.Shared.Rent()

    let fh = { Length = 648u; Flags = 3uy; Type = FrameType.DATA; StreamId = 45u }

    FrameHeader.encode fh memOwner.Memory.Span |> should equal 9

    FrameHeader.decode(memOwner.Memory.Span.Slice(0, 9)) |> should equal fh
