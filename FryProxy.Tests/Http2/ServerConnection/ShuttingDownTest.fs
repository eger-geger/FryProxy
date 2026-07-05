module FryProxy.Tests.Http2.ServerConnection.ShuttingDownTest

open System
open FryProxy.IO
open FryProxy.Http2
open FryProxy.Http2.Frames
open NUnit.Framework


let shuttingDownCnx =
    { ServerConnection.Empty with
        LastStreamId = ValueSome 9u
        NextStreamId = 5u
        ActiveStreams = [ { Id = 1u; State = StreamState.Open } ] }

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

        Frame.headers 7u ReadOnlyMemory.Empty
        |> TestCaseData
        |> _.Returns(Transition.error ErrorCode.REFUSED_STREAM)
        |> _.SetName("new stream with lower stream Id")

        Frame.headers 11u ReadOnlyMemory.Empty
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
        |> _.Returns(Transition.content (MemoryByteSeq()) shuttingDownCnx)
        |> _.SetName("remaining data")

        Frame.priority 1u
        |> TestCaseData
        |> _.Returns(Transition.pending shuttingDownCnx)
        |> _.SetName("priority frame")

        Frame.ping pingBody
        |> TestCaseData
        |> _.Returns(Transition.ping (MemoryByteSeq pingBody) shuttingDownCnx)
        |> _.SetName("ping frame")
    }


[<TestCaseSource(nameof transitionTestCases)>]
let testTransition frame =
    ServerConnection.transition shuttingDownCnx frame
