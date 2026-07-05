module FryProxy.Tests.Http2.IdleTransitionTests

#nowarn "3391"

open System
open FryProxy.IO
open FryProxy.Extension
open FryProxy.Http
open FryProxy.Http2
open FryProxy.Http2.Frames
open FryProxy.Http2.Hpack
open NUnit.Framework

let pingBody = ReadOnlyMemory(Array.zeroCreate 8)

let transitionTestCases =
    let invalidFrames =
        [ Frame.emptyData 1u
          Frame.pushPromise 1u
          Frame.continuation 1u ReadOnlyMemory.Empty
          Frame.windowUpdate 1u 0u
          Frame.settings 1u List.Empty
          Frame.reset 1u ErrorCode.NO_ERROR ]

    let connWithOpenStream =
        { ServerConnection.Empty with
            NextStreamId = 3u
            ActiveStreams = [ { Id = 1u; State = StreamState.Open } ] }

    let connWithClosedStream =
        { ServerConnection.Empty with
            NextStreamId = 3u
            ActiveStreams = [ { Id = 1u; State = StreamState.HalfClosed } ] }

    let fields =
        [ { Name = ":method"; Value = "GET" }
          { Name = ":scheme"; Value = "https" }
          { Name = ":path"; Value = "/resources" }
          { Name = ":authority"; Value = "example.com" } ]
        |> List.map FieldPack.Default

    let struct (fieldBlock, table) = fields |> Table.encodeFields Table.empty

    seq {
        for frame in invalidFrames do
            yield
                TestCaseData(frame)
                    .Returns(Transition.error ErrorCode.PROTOCOL_ERROR)
                    .SetName($"invalid frame type {frame.Header.Type}")

        yield
            TestCaseData(Frame.ping ReadOnlyMemory.Empty)
                .Returns(Transition.error ErrorCode.FRAME_SIZE_ERROR)
                .SetName("empty ping frame")

        yield
            TestCaseData(Frame.ping (ReadOnlyMemory(Array.zeroCreate 9)))
                .Returns(Transition.error ErrorCode.FRAME_SIZE_ERROR)
                .SetName("ping frame too long")

        yield
            TestCaseData({ Frame.ping pingBody with Header.StreamId = 1u })
                .Returns(Transition.error ErrorCode.PROTOCOL_ERROR)
                .SetName("invalid ping stream Id")

        yield
            TestCaseData(Frame.ping pingBody)
                .Returns(Transition.ping (MemoryByteSeq pingBody) ServerConnection.Empty)
                .SetName("ping request")

        yield
            TestCaseData(Frame.ping pingBody |> Frame.withFlags PingFlags.ACK)
                .Returns(Transition.pending ServerConnection.Empty)
                .SetName("ping ack")

        yield
            TestCaseData(Frame.headers 1u ReadOnlyMemory.Empty)
                .Returns(Transition.pendingFields 1u SizedBuffer.Empty connWithOpenStream)
                .SetName("empty headers frame")

        yield
            TestCaseData(Frame.goAway 1u ErrorCode.NO_ERROR)
                .Returns(Transition.pending ServerConnection.Empty)
                .SetName("go away")

        yield
            TestCaseData({ Frame.goAway 1u ErrorCode.NO_ERROR with Header.StreamId = 1u })
                .Returns(Transition.error ErrorCode.PROTOCOL_ERROR)
                .SetName("go away non-zero stream Id")

        yield
            TestCaseData(Frame.goAway 1u ErrorCode.COMPRESSION_ERROR)
                .Returns(Transition.close ErrorCode.COMPRESSION_ERROR ServerConnection.Empty)
                .SetName("go away with error")

        yield
            Frame.headers 1u (fieldBlock.Slice(5))
            |> Frame.withFlags HeadersFlags.END_HEADERS
            |> TestCaseData
            |> _.Returns(Transition.error ErrorCode.COMPRESSION_ERROR)
            |> _.SetName("truncated field block")

        yield
            TestCaseData(Frame.headers 1u fieldBlock)
                .Returns(Transition.pendingFields 1u (SizedBuffer.From fieldBlock) connWithOpenStream)
                .SetName("incomplete headers frame")

        yield
            Frame.headers 1u fieldBlock
            |> Frame.withFlags HeadersFlags.END_HEADERS
            |> TestCaseData
            |> _.Returns(Transition.decodedFields fields table connWithOpenStream)
            |> _.SetName("complete headers frame")

        yield
            Frame.headers 1u fieldBlock
            |> Frame.withFlags (HeadersFlags.END_HEADERS ||| HeadersFlags.END_STREAM)
            |> TestCaseData
            |> _.Returns(Transition.decodedFields fields table connWithClosedStream)
            |> _.SetName("complete headers frame closing stream")

        yield
            TestCaseData(Frame.priority 1u)
                .Returns(Transition.pending ServerConnection.Empty)
                .SetName("empty priority frame")
    }

[<TestCaseSource(nameof transitionTestCases)>]
let testTransition frame =
    frame |> ServerConnection.transition ServerConnection.Empty
