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

let transitionTestCases =
    let trailerFields =
        [ { Name = "grpc-status"; Value = "0" }
          { Name = "grpc-message"; Value = "OK" } ]
        |> List.map FieldPack.Default

    let trailerBytes, trailerTable = Table.encodeFieldsMem openingTable trailerFields

    let openCnx =
        { ServerConnection.Empty with
            NextStreamId = 3u
            HPackTable = openingTable
            ActiveStreams = Map.ofList [ (1u, H2Stream.Open 1u) ] }

    let binaryData = ReadOnlyMemory([| 0uy; 1uy; 2uy; 3uy; 4uy; 5uy; 6uy; 7uy |])

    seq {
        Frame.headers 3u ReadOnlyMemory.Empty
        |> TestCaseData
        |> _.Returns(
            { openCnx with
                NextStreamId = 5u
                ActiveStreams = openCnx.ActiveStreams.Add(3u, H2Stream.Open 3u) }
            |> ConnectionState.withPendingHeader 3u SizedBuffer.Empty
            |> Transition.pending
        )
        |> _.SetName("open another stream")

        Frame.priority 1u
        |> TestCaseData
        |> _.Returns(Transition.pending openCnx)
        |> _.SetName("noop priority frame")

        Frame.headers 1u trailerBytes
        |> Frame.withFlags HeadersFlags.END_HEADERS
        |> TestCaseData
        |> _.Returns(
            Transition.trailerFields
                (H2Stream.Open 1u)
                trailerFields
                { openCnx with HPackTable = trailerTable; PendingFields = ValueNone }
        )
        |> _.SetName("complete trailer header frame")

        Frame.headers 1u ReadOnlyMemory.Empty
        |> TestCaseData
        |> _.Returns(
            openCnx
            |> ConnectionState.withPendingTrailer 1u SizedBuffer.Empty
            |> Transition.pending
        )
        |> _.SetName("empty trailer header frame")

        Frame.binaryData 1u binaryData
        |> TestCaseData
        |> _.Returns(Transition.content (H2Stream.Open 1u) (MemoryByteSeq binaryData) openCnx)
        |> _.SetName("data frame")

        Frame.continuation 1u ReadOnlyMemory.Empty
        |> TestCaseData
        |> _.Returns(Transition.connectionError ErrorCode.PROTOCOL_ERROR openCnx)
        |> _.SetName("unexpected continuation frame")

        Frame.reset 1u ErrorCode.CANCEL
        |> TestCaseData
        |> _.Returns(
            { openCnx with ActiveStreams = Map.empty; ResetStreams = Set.singleton 1u }
            |> Transition.streamClosed 1u
        )
        |> _.SetName("reset stream")

        Frame.windowUpdate 1u 0u
        |> TestCaseData
        |> _.Returns(
            Transition.streamError
                ErrorCode.PROTOCOL_ERROR
                1u
                { openCnx with ActiveStreams = Map.empty; ResetStreams = Set.singleton 1u }
        )
        |> _.SetName("window update zero increment")

        Frame.windowUpdate 1u 10u
        |> TestCaseData
        |> _.Returns(Transition.pending openCnx)
        |> _.SetName("window update")

        Frame.pushPromise 1u
        |> TestCaseData
        |> _.Returns(Transition.connectionError ErrorCode.PROTOCOL_ERROR openCnx)
        |> _.SetName("push promise")
    }

[<TestCaseSource(nameof transitionTestCases)>]
let testTransition frame =
    let zeroCnx = { ServerConnection.Empty with HPackTable = zeroTable }

    let struct (cnx, _) =
        Frame.headers 1u openingHeaderBytes
        |> Frame.withFlags HeadersFlags.END_HEADERS
        |> ServerConnection.transition zeroCnx

    ServerConnection.transition cnx frame
