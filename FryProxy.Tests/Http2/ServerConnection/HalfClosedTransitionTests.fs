module FryProxy.Tests.Http2.ServerConnection.HalfClosedTransitionTests

open System
open FryProxy.Extension
open FryProxy.Http
open FryProxy.Http2
open FryProxy.Http2.Frames
open FryProxy.Http2.Hpack
open NUnit.Framework

let halfClosedCnx =
    { ServerConnection.Empty with
        NextStreamId = 3u
        ActiveStreams = Map.ofList [ (1u, H2Stream.HalfClosed 1u) ] }

let transitionTestCases =
    let trailerFields =
        [ { Name = "grpc-status"; Value = "0" }
          { Name = "grpc-message"; Value = "OK" } ]
        |> List.map FieldPack.Default

    let openingTable = Table.empty
    let trailerBytes, trailerTable = Table.encodeFieldsMem openingTable trailerFields

    seq {
        // RST_STREAM on half-closed stream should close the stream (remove from ActiveStreams)
        Frame.reset 1u ErrorCode.CANCEL
        |> TestCaseData
        |> _.Returns(
            { halfClosedCnx with ActiveStreams = Map.empty; ResetStreams = Set.singleton 1u }
            |> Transition.streamClosed 1u
        )
        |> _.SetName("reset stream closes half-closed stream")

        // WINDOW_UPDATE on half-closed stream is accepted
        Frame.windowUpdate 1u 10u
        |> TestCaseData
        |> _.Returns(Transition.pending halfClosedCnx)
        |> _.SetName("window update")

        // WINDOW_UPDATE with zero increment is a protocol error
        Frame.windowUpdate 1u 0u
        |> TestCaseData
        |> _.Returns(
            Transition.streamError
                ErrorCode.PROTOCOL_ERROR
                1u
                { halfClosedCnx with ActiveStreams = Map.empty; ResetStreams = Set.singleton 1u }
        )
        |> _.SetName("window update zero increment")

        // PRIORITY is accepted
        Frame.priority 1u
        |> TestCaseData
        |> _.Returns(Transition.pending halfClosedCnx)
        |> _.SetName("priority frame")

        // HEADERS (trailers) are accepted
        Frame.headers 1u trailerBytes
        |> Frame.withFlags HeadersFlags.END_HEADERS
        |> TestCaseData
        |> _.Returns(
            Transition.fields (H2Stream.HalfClosed 1u) trailerFields false { halfClosedCnx with HPackTable = trailerTable }
        )
        |> _.SetName("trailer headers")

        // DATA is accepted
        let binaryData = ReadOnlyMemory([| 0uy; 1uy; 2uy; 3uy |])
        Frame.binaryData 1u binaryData
        |> TestCaseData
        |> _.Returns(
            Transition.content 1u (FryProxy.IO.MemoryByteSeq binaryData) false halfClosedCnx
        )
        |> _.SetName("data frame")

        // Push promise is a protocol error
        Frame.pushPromise 1u
        |> TestCaseData
        |> _.Returns(Transition.connectionError ErrorCode.PROTOCOL_ERROR halfClosedCnx)
        |> _.SetName("push promise")
    }

[<TestCaseSource(nameof transitionTestCases)>]
let testTransition frame =
    ServerConnection.transition halfClosedCnx frame
