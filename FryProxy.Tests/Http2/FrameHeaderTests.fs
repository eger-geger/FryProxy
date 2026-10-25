module FryProxy.Tests.Http2.FrameHeaderTests

open System.Buffers
open FryProxy.Http2
open FryProxy.Http2.Frames
open FsUnit
open NUnit.Framework

#nowarn 3391

[<Test>]
let testFrameTypeSize () =
    sizeof<FrameBody> |> should equal 8
    sizeof<FrameHeader> |> should equal 12
    sizeof<Frame> |> should equal 24

[<Test>]
let testEncodeDecode () =
    use memOwner = MemoryPool.Shared.Rent()

    let fh = { Length = 648u; Flags = 3uy; Type = FrameType.DATA; StreamId = 45u }

    FrameHeader.encode fh memOwner.Memory.Span |> should equal 9

    FrameHeader.decode (memOwner.Memory.Span.Slice(0, 9)) |> should equal fh

[<TestCase(FrameType.PING, 0uy, 1u, 8u, "PING { #1[8] }")>]
[<TestCase(FrameType.PING, 1uy, 1u, 8u, "PING { #1[8] ACK }")>]
[<TestCase(FrameType.DATA, 0uy, 5u, 1024u, "DATA { #5[1024] }")>]
[<TestCase(FrameType.DATA, 1uy, 5u, 1024u, "DATA { #5[1024] END_STREAM }")>]
[<TestCase(FrameType.DATA, 9uy, 5u, 1024u, "DATA { #5[1024] END_STREAM|PADDED }")>]
[<TestCase(FrameType.HEADERS, 5uy, 1u, 24u, "HEADERS { #1[24] END_STREAM|END_HEADERS }")>]
[<TestCase(FrameType.HEADERS, 37uy, 3u, 50u, "HEADERS { #3[50] END_STREAM|END_HEADERS|PRIORITY }")>]
[<TestCase(FrameType.SETTINGS, 0uy, 0u, 12u, "SETTINGS { #0[12] }")>]
[<TestCase(FrameType.SETTINGS, 1uy, 0u, 0u, "SETTINGS { #0[0] ACK }")>]
[<TestCase(FrameType.PUSH_PROMISE, 12uy, 2u, 100u, "PUSH_PROMISE { #2[100] END_HEADERS|PADDED }")>]
[<TestCase(FrameType.CONTINUATION, 4uy, 1u, 42u, "CONTINUATION { #1[42] END_HEADERS }")>]
[<TestCase(FrameType.RST_STREAM, 0uy, 1u, 4u, "RST_STREAM { #1[4] }")>]
[<TestCase(FrameType.RST_STREAM, 2uy, 1u, 4u, "RST_STREAM { #1[4] 0x02 }")>]
let testFormat (frameType: FrameType) (flags: byte) (streamId: uint32) (length: uint32) (expected: string) =
    let fh = { Type = frameType; Flags = flags; StreamId = streamId; Length = length }
    
    FrameHeader.format fh |> should equal expected
    fh.ToString() |> should equal expected
    $"%A{fh}" |> should equal expected
