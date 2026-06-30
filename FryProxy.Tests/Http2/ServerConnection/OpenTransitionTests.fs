module FryProxy.Tests.Http2.ServerConnection.OpenTransitionTests

#nowarn "3391"

open System
open FryProxy.IO
open FryProxy.Extension
open FryProxy.Http
open FryProxy.Http2
open FryProxy.Http2.Frames
open FryProxy.Http2.Hpack
open NUnit.Framework

let struct (openingHeaderBytes, openingTable) =
    [ { Name = ":method"; Value = "GET" }
      { Name = ":scheme"; Value = "https" }
      { Name = ":path"; Value = "/" }
      { Name = ":authority"; Value = "example.com" } ]
    |> Table.encodeRawFieldsDefault Table.empty

let transitionTestCases =
    let trailerFields =
        [ { Name = "grpc-status"; Value = "0" }
          { Name = "grpc-message"; Value = "OK" } ]
        |> List.map FieldPack.Default

    let struct (trailerBytes, trailerTable) =
        trailerFields |> Table.encodeFields openingTable

    let openCnx =
        { NextStreamId = 3u
          HPackTable = openingTable
          PendingHeader = ValueNone
          Streams = [ { Id = 1u; State = StreamState.Open } ] }

    let binaryData = MemoryByteSeq([| 0uy; 1uy; 2uy; 3uy; 4uy; 5uy; 6uy; 7uy |])

    seq {
        Frame.headers 3u ReadOnlyMemory.Empty
        |> TestCaseData
        |> _.Returns(
            { openCnx with
                NextStreamId = 5u
                Streams = { Id = 3u; State = StreamState.Open } :: openCnx.Streams }
            |> Transition.pendingFields 3u SizedBuffer.Empty
        )
        |> _.SetName("open another stream")

        Frame.priority 1u
        |> TestCaseData
        |> _.Returns(Transition.pending openCnx)
        |> _.SetName("noop priority frame")

        Frame.headers 1u trailerBytes
        |> Frame.withFlags HeadersFlags.END_HEADERS
        |> TestCaseData
        |> _.Returns(Transition.fields trailerFields { openCnx with HPackTable = trailerTable })
        |> _.SetName("complete trailer header frame")

        Frame.headers 1u ReadOnlyMemory.Empty
        |> TestCaseData
        |> _.Returns(Transition.pendingFields 1u SizedBuffer.Empty { openCnx with HPackTable = trailerTable })
        |> _.SetName("empty trailer header frame")

        Frame.binaryData 1u binaryData.Memory
        |> TestCaseData
        |> _.Returns(Transition.content binaryData openCnx)
        |> _.SetName("data frame")

        Frame.continuation 1u ReadOnlyMemory.Empty
        |> TestCaseData
        |> _.Returns(Transition.error ErrorCode.PROTOCOL_ERROR)
        |> _.SetName("unexpected continuation frame")

        Frame.reset 1u ErrorCode.CANCEL
        |> TestCaseData
        |> _.Returns(Transition.reset ErrorCode.CANCEL { openCnx with Streams = [] })
        |> _.SetName("reset stream")
    }

[<TestCaseSource(nameof transitionTestCases)>]
let testTransition frame =
    let struct (_, cnx) =
        Frame.headers 1u openingHeaderBytes
        |> Frame.withFlags HeadersFlags.END_HEADERS
        |> ServerConnection.transition ServerConnection.Empty
        |> Result.defaultWith (fun _ -> failwith "transition transition failed")

    ServerConnection.transition cnx frame
