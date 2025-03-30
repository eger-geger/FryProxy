module FryProxy.Tests.Http2.ServerConnectionTests

#nowarn "3391"

open System
open FryProxy.Extension
open FryProxy.Http
open FryProxy.Http2
open FryProxy.Http2.Frames
open FryProxy.Http2.Hpack
open NUnit.Framework

let protocolError = Transition.error ErrorCode.PROTOCOL_ERROR

let idleInboundStreamTransitionTestCases =
    let streamId = 2u

    let invalidFrames =
        [ Frame.ping streamId
          Frame.emptyData streamId
          Frame.pushPromise streamId
          Frame.continuation streamId
          Frame.windowUpdate streamId 0u
          Frame.settings streamId List.Empty
          Frame.reset streamId ErrorCode.NO_ERROR
          Frame.goAway streamId ErrorCode.NO_ERROR ]

    let connWithOpenStream =
        { ServerConnection.Empty with
            Streams = [ { Id = streamId; State = StreamState.Open } ] }

    let connWithClosedStream =
        { ServerConnection.Empty with
            Streams = [ { Id = streamId; State = StreamState.Closed } ] }

    let fields =
        [ { Name = ":method"; Value = "GET" }
          { Name = ":scheme"; Value = "https" }
          { Name = ":path"; Value = "/resources" }
          { Name = ":authority"; Value = "example.com" } ]
        |> List.map FieldPack.Default

    let struct (fieldBlock, _) = fields |> Table.encodeFields Table.empty

    seq {
        for frame in invalidFrames do
            yield
                TestCaseData(frame)
                    .Returns(protocolError)
                    .SetName($"invalid frame type {frame.Header.Type}")

        yield
            TestCaseData(Frame.headers streamId ReadOnlyMemory.Empty)
                .Returns(Transition.pending connWithOpenStream)
                .SetName("empty headers frame")

        yield
            Frame.headers streamId (fieldBlock.Slice(5))
            |> Frame.withFlags HeadersFlags.END_HEADERS
            |> TestCaseData
            |> _.Returns(Transition.error ErrorCode.COMPRESSION_ERROR)
            |> _.SetName("truncated field block")

        yield
            TestCaseData(Frame.headers streamId fieldBlock)
                .Returns(Transition.pending { connWithOpenStream with PendingFieldBuffer = fieldBlock.UnitOwner() })
                .SetName("incomplete headers frame")

        yield
            Frame.headers streamId fieldBlock
            |> Frame.withFlags HeadersFlags.END_HEADERS
            |> TestCaseData
            |> _.Returns(Transition.headers fields connWithOpenStream)
            |> _.SetName("complete headers frame")

        yield
            Frame.headers streamId fieldBlock
            |> Frame.withFlags(HeadersFlags.END_HEADERS ||| HeadersFlags.END_STREAM)
            |> TestCaseData
            |> _.Returns(Transition.headers fields connWithClosedStream)
            |> _.SetName("complete headers frame closing stream")

        yield
            TestCaseData(Frame.priority streamId)
                .Returns(Transition.pending connWithOpenStream)
                .SetName("empty priority frame")
    }

[<TestCaseSource(nameof idleInboundStreamTransitionTestCases)>]
let testIdleStreamTransition frame =
    frame |> ServerConnection.transition ServerConnection.Empty
