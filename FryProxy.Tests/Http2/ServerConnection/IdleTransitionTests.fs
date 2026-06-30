module FryProxy.Tests.Http2.IdleTransitionTests

#nowarn "3391"

open System
open FryProxy.Extension
open FryProxy.Http
open FryProxy.Http2
open FryProxy.Http2.Frames
open FryProxy.Http2.Hpack
open NUnit.Framework

let idleInboundStreamTransitionTestCases =
    let invalidFrames =
        [ Frame.ping 1u
          Frame.emptyData 1u
          Frame.pushPromise 1u
          Frame.continuation 1u ReadOnlyMemory.Empty
          Frame.windowUpdate 1u 0u
          Frame.settings 1u List.Empty
          Frame.reset 1u ErrorCode.NO_ERROR
          Frame.goAway 1u ErrorCode.NO_ERROR ]

    let connWithOpenStream =
        { ServerConnection.Empty with
            NextStreamId = 3u
            Streams = [ { Id = 1u; State = StreamState.Open } ] }

    let connWithClosedStream =
        { ServerConnection.Empty with
            NextStreamId = 3u
            Streams = [ { Id = 1u; State = StreamState.HalfClosed } ] }

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
            TestCaseData(Frame.headers 1u ReadOnlyMemory.Empty)
                .Returns(Transition.pendingFields 1u SizedBuffer.Empty connWithOpenStream)
                .SetName("empty headers frame")

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
                .Returns(Transition.pending connWithOpenStream)
                .SetName("empty priority frame")
    }

[<TestCaseSource(nameof idleInboundStreamTransitionTestCases)>]
let testTransition frame =
    frame |> ServerConnection.transition ServerConnection.Empty
