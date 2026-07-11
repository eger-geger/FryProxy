module FryProxy.Tests.Http2.ServerConnection.ClosedTransitionTests

open System
open FryProxy.Http2
open FryProxy.Http2.Frames
open NUnit.Framework

let closedCnx = { ServerConnection.Empty with NextStreamId = 9u }

let resetCnx = { closedCnx with ResetStreams = set [ 1u ] }

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
        |> _.Returns(Transition.error ErrorCode.STREAM_CLOSED)
        |> _.SetName("headers")

        Frame.emptyData 1u
        |> TestCaseData
        |> _.Returns(Transition.error ErrorCode.STREAM_CLOSED)
        |> _.SetName("data")

    }

[<TestCaseSource(nameof transitionTestCases)>]
let testTransition frame =
    ServerConnection.transition closedCnx frame

let ignoreResetTestCases =
    seq {
        Frame.headers 1u ReadOnlyMemory.Empty
        |> Frame.withFlags HeadersFlags.END_HEADERS
        |> TestCaseData
        |> _.Returns(Transition.pending resetCnx)
        |> _.SetName("headers")

        Frame.emptyData 1u
        |> TestCaseData
        |> _.Returns(Transition.pending resetCnx)
        |> _.SetName("data")
    }

[<TestCaseSource(nameof ignoreResetTestCases)>]
let testIgnoresDataFramesAfterReset frame =
    ServerConnection.transition resetCnx frame
