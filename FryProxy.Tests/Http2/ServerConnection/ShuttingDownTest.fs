module FryProxy.Tests.Http2.ServerConnection.ShuttingDownTest

open System
open FryProxy.Http2
open FryProxy.Http2.Frames
open NUnit.Framework


let shuttingDownCnx =
    { ServerConnection.Empty with
        LastStreamId = ValueSome 9u
        NextStreamId = 13u
        ActiveStreams = Map.ofList [ (1u, StreamState.Open); (11u, StreamState.Open) ] }

let transitionTestCases =
    let pingBody = ReadOnlyMemory([| 0uy; 1uy; 2uy; 3uy; 4uy; 5uy; 6uy; 7uy |])

    seq {
        Frame.goAway 1u ErrorCode.NO_ERROR
        |> TestCaseData
        |> _.Returns(Transition.pending shuttingDownCnx)
        |> _.SetName("second go away")

        Frame.goAway 1u ErrorCode.PROTOCOL_ERROR
        |> TestCaseData
        |> _.Returns(Transition.close ErrorCode.PROTOCOL_ERROR shuttingDownCnx)
        |> _.SetName("second go away with error")

        Frame.headers 13u ReadOnlyMemory.Empty
        |> TestCaseData
        |> _.Returns(Transition.error ErrorCode.REFUSED_STREAM)
        |> _.SetName("new stream with higher stream Id")

        Frame.headers 1u ReadOnlyMemory.Empty
        |> Frame.withFlags HeadersFlags.END_HEADERS
        |> TestCaseData
        |> _.Returns(Transition.fields [] shuttingDownCnx)
        |> _.SetName("remining headers")

        Frame.emptyData 1u
        |> TestCaseData
        |> _.Returns(Transition.content ReadOnlyMemory.Empty shuttingDownCnx)
        |> _.SetName("remaining data")

        Frame.emptyData 11u
        |> Frame.withFlags HeadersFlags.END_STREAM
        |> TestCaseData
        |> _.Returns(Transition.error ErrorCode.REFUSED_STREAM)
        |> _.SetName("remaining data on ignored stream")

        Frame.priority 1u
        |> TestCaseData
        |> _.Returns(Transition.pending shuttingDownCnx)
        |> _.SetName("priority frame")

        Frame.ping pingBody
        |> TestCaseData
        |> _.Returns(Transition.ping pingBody shuttingDownCnx)
        |> _.SetName("ping frame")

        Frame.windowUpdate 0u 10u
        |> TestCaseData
        |> _.Returns(Transition.connectionWindowUpdate 10u shuttingDownCnx)
        |> _.SetName("connection windows update")

        Frame.windowUpdate 1u 8u
        |> TestCaseData
        |> _.Returns(Transition.streamWindowUpdate 1u 8u shuttingDownCnx)
        |> _.SetName("stream windows update")

        Frame.windowUpdate 11u 8u
        |> TestCaseData
        |> _.Returns(Transition.error ErrorCode.REFUSED_STREAM)
        |> _.SetName("stream windows update on ignored stream")
    }


[<TestCaseSource(nameof transitionTestCases)>]
let testTransition frame =
    ServerConnection.transition shuttingDownCnx frame
