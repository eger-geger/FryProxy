module FryProxy.Tests.Http2.ServerConnection.HalfClosedTransitionTests

#nowarn "3391"

open System
open FryProxy.Extension
open FryProxy.Http
open FryProxy.Http2
open FryProxy.Http2.Frames
open FryProxy.Http2.Hpack
open FryProxy.IO
open FsUnitTyped
open NUnit.Framework

let createHalfClosedCnx h2s =
    { ServerConnection.Empty with
        NextStreamId = h2s.Id + 2u
        ActiveStreams = Map.ofList [ (1u, h2s) ]
        OpenedStreams = [ (h2s.Id, h2s.Id) ] }

let transitionTestCases =
    let trailerFields =
        [ { Name = "grpc-status"; Value = "0" }
          { Name = "grpc-message"; Value = "OK" } ]
        |> List.map FieldPack.NotIndexed

    let trailerBytes, trailerTable = Table.encodeFieldsMem Table.empty trailerFields
    let remote = H2Stream.HalfClosedRemote 1u
    let local = H2Stream.HalfClosedLocal 1u

    let tc (name: string) (initial: H2Stream) (frame: Frame) (expected: ConnectionStateTransition) =
        TestCaseData(initial, frame, ExpectedResult = expected, TestName = name)

    let closedCnx h2s =
        { createHalfClosedCnx h2s with ActiveStreams = Map.empty }

    let streamError h2s errorCode =
        Transition.streamError errorCode h2s.Id (closedCnx h2s)

    let binaryData = ReadOnlyMemory([| 0uy; 1uy; 2uy; 3uy |])

    let validTrailerTransition =
        Transition.trailerFields (H2Stream.Closed 1u) trailerFields
        <| { closedCnx local with HPackTable = trailerTable }

    [ tc "reset stream closes half-closed-remote stream"
      <| remote
      <| Frame.reset 1u ErrorCode.CANCEL
      <| Transition.streamClosed 1u (closedCnx remote)

      tc "window update zero increment on half-closed-remote stream"
      <| remote
      <| Frame.windowUpdate 1u 0u
      <| streamError remote ErrorCode.PROTOCOL_ERROR

      tc "priority frame on half-closed-remote stream"
      <| remote
      <| Frame.priority 1u
      <| Transition.pending (createHalfClosedCnx remote)

      tc "headers on half-closed-remote stream"
      <| remote
      <| (Frame.headers 1u trailerBytes |> Frame.withFlags HeadersFlags.END_HEADERS)
      <| streamError remote ErrorCode.STREAM_CLOSED

      tc "data on half-closed-remote stream"
      <| remote
      <| Frame.binaryData 1u binaryData
      <| streamError remote ErrorCode.STREAM_CLOSED

      tc "push promise on half-closed-remote stream"
      <| remote
      <| Frame.pushPromise 1u
      <| Transition.connectionError ErrorCode.PROTOCOL_ERROR (createHalfClosedCnx remote)

      tc "valid trailer headers close half-closed-local stream"
      <| local
      <| (Frame.headers 1u trailerBytes
          |> Frame.withFlags HeadersFlags.END_STREAM
          |> Frame.withFlags HeadersFlags.END_HEADERS)
      <| validTrailerTransition

      tc "trailer headers without end stream reset half-closed-local stream"
      <| local
      <| (Frame.headers 1u trailerBytes |> Frame.withFlags HeadersFlags.END_HEADERS)
      <| streamError local ErrorCode.PROTOCOL_ERROR ]

[<TestCaseSource(nameof transitionTestCases)>]
let testTransition initialStream frame =
    ServerConnection.transition (createHalfClosedCnx initialStream) frame

[<Test>]
let testTrailerContinuationClosesHalfClosedLocalStream () =
    let trailerFields =
        [ { Name = "grpc-status"; Value = "0" } ] |> List.map FieldPack.NotIndexed

    let trailerBytes, trailerTable = Table.encodeFieldsMem Table.empty trailerFields

    let halfClosedLocalCnx = createHalfClosedCnx (H2Stream.HalfClosedLocal 1u)

    let closedCnx =
        { halfClosedLocalCnx with ActiveStreams = Map.empty }
        |> ConnectionState.withPendingTrailer 1u (SizedBuffer.From trailerBytes)

    Frame.headers 1u trailerBytes
    |> Frame.withFlags HeadersFlags.END_STREAM
    |> ServerConnection.transition halfClosedLocalCnx
    |> shouldEqual (Transition.pending closedCnx)

    Frame.continuation 1u ReadOnlyMemory.Empty
    |> Frame.withFlags ContinuationFlags.END_HEADERS
    |> ServerConnection.transition closedCnx
    |> shouldEqual (
        Transition.trailerFields (H2Stream.Closed 1u) trailerFields
        <| { closedCnx with PendingFields = ValueNone; HPackTable = trailerTable }
    )

[<Test>]
let testWindowUpdateIncrementsHalfClosedStreamWindow () =
    let cnx = createHalfClosedCnx (H2Stream.HalfClosedRemote 1u)
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
    let cnx = createHalfClosedCnx (H2Stream.HalfClosedRemote 1u)
    let stream = Map.find 1u cnx.ActiveStreams
    stream.Window.TryUpdateSize(Int32.MaxValue) |> shouldEqual true

    Frame.windowUpdate 1u 1u
    |> ServerConnection.transition cnx
    |> shouldEqual (
        Transition.streamError ErrorCode.FLOW_CONTROL_ERROR 1u
        <| { cnx with ActiveStreams = Map.empty }
    )

[<Test>]
let testLocalEndClosesRemoteHalfClosedStream () =
    let cnx = createHalfClosedCnx (H2Stream.HalfClosedRemote 1u)
    let closed = { cnx with ActiveStreams = Map.empty }

    ServerConnection.closeLocalStream 1u cnx |> shouldEqual closed
    ServerConnection.closeLocalStream 1u closed |> shouldEqual closed
    ServerConnection.closeLocalStream 3u cnx |> shouldEqual cnx

    Frame.emptyData 1u
    |> ServerConnection.transition closed
    |> shouldEqual (Transition.connectionError ErrorCode.STREAM_CLOSED closed)

[<Test>]
let testLocalEndDoesNotCloseOpenRequest () =
    let cnx =
        { ServerConnection.Empty with
            NextStreamId = 3u
            ActiveStreams = Map.ofList [ 1u, H2Stream.Open 1u ] }

    let halfClosedLocal =
        { cnx with ActiveStreams = Map.ofList [ 1u, H2Stream.HalfClosedLocal 1u ] }

    ServerConnection.closeLocalStream 1u cnx |> shouldEqual halfClosedLocal

    Frame.emptyData 1u
    |> ServerConnection.transition halfClosedLocal
    |> shouldEqual (Transition.content (H2Stream.HalfClosedLocal 1u) ByteBuffer.empty halfClosedLocal)
