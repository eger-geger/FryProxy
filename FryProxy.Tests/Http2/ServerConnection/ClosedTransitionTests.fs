module FryProxy.Tests.Http2.ServerConnection.ClosedTransitionTests

open System
open FryProxy.Http2
open FryProxy.Http2.Frames
open NUnit.Framework

let closedCnx = { ServerConnection.Empty with NextStreamId = 9u }

let openedCnx = { closedCnx with OpenedStreams = [ (1u, 1u) ] }

let transitionTestCases =
    seq {
        Frame.priority 3u
        |> TestCaseData
        |> _.Returns(Transition.pending closedCnx)
        |> _.SetName("priority")

        Frame.reset 1u ErrorCode.CANCEL
        |> TestCaseData
        |> _.Returns(Transition.pending closedCnx)
        |> _.SetName("reset")

        Frame.windowUpdate 1u 10u
        |> TestCaseData
        |> _.Returns(Transition.pending closedCnx)
        |> _.SetName("window update")

        Frame.headers 1u ReadOnlyMemory.Empty
        |> Frame.withFlags HeadersFlags.END_HEADERS
        |> TestCaseData
        |> _.Returns(Transition.connectionError ErrorCode.PROTOCOL_ERROR closedCnx)
        |> _.SetName("headers on never-opened stream")

        Frame.emptyData 1u
        |> TestCaseData
        |> _.Returns(Transition.connectionError ErrorCode.PROTOCOL_ERROR closedCnx)
        |> _.SetName("data on never-opened stream")

    }

[<TestCaseSource(nameof transitionTestCases)>]
let testTransition frame =
    ServerConnection.transition closedCnx frame

let closedStreamTestCases =
    seq {
        // Stream 1 was opened and closed: frames produce STREAM_CLOSED
        Frame.headers 1u ReadOnlyMemory.Empty
        |> Frame.withFlags HeadersFlags.END_HEADERS
        |> TestCaseData
        |> _.Returns(Transition.connectionError ErrorCode.STREAM_CLOSED openedCnx)
        |> _.SetName("headers on closed stream")

        Frame.emptyData 1u
        |> TestCaseData
        |> _.Returns(Transition.connectionError ErrorCode.STREAM_CLOSED openedCnx)
        |> _.SetName("data on closed stream")
    }

[<TestCaseSource(nameof closedStreamTestCases)>]
let testFramesOnClosedStream frame =
    ServerConnection.transition openedCnx frame
