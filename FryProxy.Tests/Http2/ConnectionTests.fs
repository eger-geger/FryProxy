module FryProxy.Tests.Http2.ConnectionTests

open FryProxy.Http2
open FryProxy.Http2.Frames

open NUnit.Framework

let protocolError = Transition.error ErrorCode.PROTOCOL_ERROR

let idleInboundStreamTransitionTestCases =
    let streamId = 2u

    let invalidFrames =
        [ FrameFactory.ping streamId
          FrameFactory.emptyData streamId
          FrameFactory.pushPromise streamId
          FrameFactory.continuation streamId
          FrameFactory.windowUpdate streamId 0u
          FrameFactory.settings streamId List.Empty
          FrameFactory.reset streamId ErrorCode.NO_ERROR
          FrameFactory.goAway streamId ErrorCode.NO_ERROR ]

    let connectionWithOpenStream =
        { Http2Connection.Empty with
            Streams = [ { Id = streamId; State = StreamState.Open } ] }

    seq {
        for frame in invalidFrames do
            yield
                TestCaseData(frame)
                    .Returns(protocolError)
                    .SetName($"invalid frame type {frame.Header.Type}")

        yield
            TestCaseData(FrameFactory.emptyHeaders streamId)
                .Ignore("unfinished")
                .Returns(Transition.pending connectionWithOpenStream)
                .SetName("empty headers frame")

        yield
            TestCaseData(FrameFactory.emptyPriority streamId)
                .Returns(Transition.pending connectionWithOpenStream)
                .SetName("empty priority frame")
    }

[<TestCaseSource(nameof idleInboundStreamTransitionTestCases)>]
let testIdleInboundStreamTransition frame =
    frame |> Http2Connection.transition Http2Connection.Empty
