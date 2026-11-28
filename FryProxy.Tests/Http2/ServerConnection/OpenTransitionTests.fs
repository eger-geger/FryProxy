module FryProxy.Tests.Http2.ServerConnection.OpenTransitionTests

#nowarn "3391"

open System
open FryProxy.Extension
open FryProxy.IO
open FryProxy.Http
open FryProxy.Http2
open FryProxy.Http2.Frames
open FryProxy.Http2.Hpack
open NUnit.Framework

let zeroTable = { Table.empty with Capacity = 0u }

let openingHeaderBytes, openingTable =
    [ { Name = ":method"; Value = "GET" }
      { Name = ":scheme"; Value = "https" }
      { Name = ":path"; Value = "/" }
      { Name = ":authority"; Value = "example.com" } ]
    |> Table.encodeRawFieldsMem zeroTable

let createOpenCnx () =
    let zeroCnx = { ServerConnection.Empty with HPackTable = zeroTable }

    let struct (cnx, _) =
        Frame.headers 1u openingHeaderBytes
        |> Frame.withFlags HeadersFlags.END_HEADERS
        |> ServerConnection.transition zeroCnx

    cnx

let transitionTestCases =
    let trailerFields =
        [ { Name = "grpc-status"; Value = "0" }
          { Name = "grpc-message"; Value = "OK" } ]
        |> List.map FieldPack.Default

    let trailerBytes, trailerTable = Table.encodeFieldsMem openingTable trailerFields

    let openCnx = createOpenCnx ()

    let binaryData = ReadOnlyMemory([| 0uy; 1uy; 2uy; 3uy; 4uy; 5uy; 6uy; 7uy |])

    let tc name (frame: Frame) (expected: ConnectionStateTransition) =
        TestCaseData([| box frame |], ExpectedResult = expected).SetName(name)

    [ tc "open another stream"
      <| Frame.headers 3u ReadOnlyMemory.Empty
      <| ({ openCnx with
              NextStreamId = 5u
              ActiveStreams = openCnx.ActiveStreams.Add(3u, H2Stream.Open 3u)
              OpenedStreams = [ (1u, 3u) ] }
          |> ConnectionState.withPendingHeader 3u SizedBuffer.Empty
          |> Transition.pending)

      tc "noop priority frame" <| Frame.priority 1u <| Transition.pending openCnx

      tc "complete trailer header frame"
      <| (Frame.headers 1u trailerBytes
          |> Frame.withFlags HeadersFlags.END_STREAM
          |> Frame.withFlags HeadersFlags.END_HEADERS)
      <| (Transition.trailerFields (H2Stream.HalfClosedRemote 1u) trailerFields
          <| { openCnx with
                 HPackTable = trailerTable
                 ActiveStreams = openCnx.ActiveStreams.Add(1u, H2Stream.HalfClosedRemote 1u)
                 PendingFields = ValueNone })

      tc "empty trailer header frame"
      <| (Frame.headers 1u ReadOnlyMemory.Empty |> Frame.withFlags HeadersFlags.END_STREAM)
      <| ({ openCnx with
              ActiveStreams = openCnx.ActiveStreams.Add(1u, H2Stream.HalfClosedRemote 1u) }
          |> ConnectionState.withPendingTrailer 1u SizedBuffer.Empty
          |> Transition.pending)

      tc "trailer header frame without end stream"
      <| (Frame.headers 1u trailerBytes |> Frame.withFlags HeadersFlags.END_HEADERS)
      <| Transition.streamError ErrorCode.PROTOCOL_ERROR 1u { openCnx with ActiveStreams = Map.empty }

      tc "data frame"
      <| Frame.binaryData 1u binaryData
      <| Transition.content (H2Stream.Open 1u) (MemoryByteSeq binaryData) openCnx

      tc "unexpected continuation frame"
      <| Frame.continuation 1u ReadOnlyMemory.Empty
      <| Transition.connectionError ErrorCode.PROTOCOL_ERROR openCnx

      tc "reset stream"
      <| Frame.reset 1u ErrorCode.CANCEL
      <| ({ openCnx with ActiveStreams = Map.empty } |> Transition.streamClosed 1u)

      tc "window update zero increment"
      <| Frame.windowUpdate 1u 0u
      <| Transition.streamError ErrorCode.PROTOCOL_ERROR 1u { openCnx with ActiveStreams = Map.empty }

      tc "window update"
      <| Frame.windowUpdate 1u 10u
      <| Transition.pending openCnx

      tc "push promise"
      <| Frame.pushPromise 1u
      <| Transition.connectionError ErrorCode.PROTOCOL_ERROR openCnx ]

[<TestCaseSource(nameof transitionTestCases)>]
let testTransition frame =
    ServerConnection.transition (createOpenCnx ()) frame
