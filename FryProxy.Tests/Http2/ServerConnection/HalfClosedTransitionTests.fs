module FryProxy.Tests.Http2.ServerConnection.HalfClosedTransitionTests

#nowarn "3391"

open System
open FryProxy.Http
open FryProxy.Http2
open FryProxy.Http2.Frames
open FryProxy.Http2.Hpack
open FsUnitTyped
open NUnit.Framework

let createHalfClosedCnx () =
    { ServerConnection.Empty with
        NextStreamId = 3u
        ActiveStreams = Map.ofList [ (1u, H2Stream.HalfClosed 1u) ] }

let transitionTestCases =
    let halfClosedCnx = createHalfClosedCnx ()

    let trailerFields =
        [ { Name = "grpc-status"; Value = "0" }
          { Name = "grpc-message"; Value = "OK" } ]
        |> List.map FieldPack.NotIndexed

    let trailerBytes, _ = Table.encodeFieldsMem Table.empty trailerFields

    seq {
        // RST_STREAM on half-closed stream should close the stream (remove from ActiveStreams)
        Frame.reset 1u ErrorCode.CANCEL
        |> TestCaseData
        |> _.Returns({ halfClosedCnx with ActiveStreams = Map.empty } |> Transition.streamClosed 1u)
        |> _.SetName("reset stream closes half-closed stream")

        // WINDOW_UPDATE with zero increment is a protocol error
        Frame.windowUpdate 1u 0u
        |> TestCaseData
        |> _.Returns(
            Transition.streamError ErrorCode.PROTOCOL_ERROR 1u
            <| { halfClosedCnx with
                   ActiveStreams = Map.empty
                   ResetStreams = Set.singleton 1u }
        )
        |> _.SetName("window update zero increment")

        // PRIORITY is accepted
        Frame.priority 1u
        |> TestCaseData
        |> _.Returns(Transition.pending halfClosedCnx)
        |> _.SetName("priority frame")

        // HEADERS are rejected after the peer closes its side of the stream.
        Frame.headers 1u trailerBytes
        |> Frame.withFlags HeadersFlags.END_HEADERS
        |> TestCaseData
        |> _.Returns(
            Transition.streamError ErrorCode.STREAM_CLOSED 1u
            <| { halfClosedCnx with
                   ActiveStreams = Map.empty
                   ResetStreams = Set.singleton 1u }
        )
        |> _.SetName("headers on half-closed stream")

        // DATA is rejected after the peer closes its side of the stream.
        let binaryData = ReadOnlyMemory([| 0uy; 1uy; 2uy; 3uy |])

        Frame.binaryData 1u binaryData
        |> TestCaseData
        |> _.Returns(
            Transition.streamError ErrorCode.STREAM_CLOSED 1u
            <| { halfClosedCnx with
                   ActiveStreams = Map.empty
                   ResetStreams = Set.singleton 1u }
        )
        |> _.SetName("data on half-closed stream")

        // Push promise is a protocol error
        Frame.pushPromise 1u
        |> TestCaseData
        |> _.Returns(Transition.connectionError ErrorCode.PROTOCOL_ERROR halfClosedCnx)
        |> _.SetName("push promise")
    }

[<TestCaseSource(nameof transitionTestCases)>]
let testTransition frame =
    ServerConnection.transition (createHalfClosedCnx ()) frame

[<Test>]
let testWindowUpdateIncrementsHalfClosedStreamWindow () =
    let cnx = createHalfClosedCnx ()
    let stream = Map.find 1u cnx.ActiveStreams

    let pending =
        stream.Window.ConsumeAsync(1u, System.Threading.CancellationToken.None)

    pending.IsCompleted |> shouldEqual false

    Frame.windowUpdate 1u 10u
    |> ServerConnection.transition cnx
    |> shouldEqual (Transition.pending cnx)

    let windowCredit =
        stream.Window.ConsumeAsync(11u, System.Threading.CancellationToken.None)

    windowCredit.IsCompletedSuccessfully |> shouldEqual true
    windowCredit.Result |> shouldEqual 10u

[<Test>]
let testWindowUpdateOverflowResetsHalfClosedStream () =
    let cnx = createHalfClosedCnx ()
    let stream = Map.find 1u cnx.ActiveStreams
    stream.Window.TryUpdateSize(Int32.MaxValue) |> shouldEqual true

    Frame.windowUpdate 1u 1u
    |> ServerConnection.transition cnx
    |> shouldEqual (
        Transition.streamError ErrorCode.FLOW_CONTROL_ERROR 1u
        <| { cnx with ActiveStreams = Map.empty; ResetStreams = Set.singleton 1u }
    )
