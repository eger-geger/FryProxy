module FryProxy.Tests.Http2.ServerConnection.PendingFieldsTests

#nowarn "3391"

open System
open FryProxy.Extension
open FryProxy.Http
open FryProxy.Http2
open FryProxy.Http2.Frames
open FryProxy.Http2.Hpack
open FsUnitTyped
open NUnit.Framework

let zeroTbl = { Table.empty with Capacity = 0u }
let zeroCnx = { ServerConnection.Empty with HPackTable = zeroTbl }

let failedTransitionTestCases =
    let firstFields =
        [ { Name = ":method"; Value = "GET" }
          { Name = ":scheme"; Value = "https" }
          { Name = ":path"; Value = "/resources" }
          { Name = ":authority"; Value = "example.com" }
          { Name = "user-agent"; Value = "Mozilla/5.0 (Windows NT 10.0; Win64; x64)" }
          { Name = "accept"; Value = "application/json, text/plain, */*" } ]
        |> List.map FieldPack.Default

    let secondFields =
        [ { Name = "accept-encoding"; Value = "gzip, deflate, br" }
          { Name = "accept-language"; Value = "en-US,en;q=0.9" }
          { Name = "cache-control"; Value = "no-cache" } ]
        |> List.map FieldPack.Default

    let headers1, table = Table.encodeFieldsMem zeroTbl firstFields
    let headers2, _ = Table.encodeFieldsMem table secondFields

    let firstFrame = Frame.headers 1u headers1

    seq {
        TestCaseData(firstFrame, Frame.headers 1u headers2).SetName("HEADERS")
        TestCaseData(firstFrame, Frame.emptyData 1u).SetName("DATA")
        TestCaseData(firstFrame, Frame.priority 1u).SetName("PRIORITY")
        TestCaseData(firstFrame, Frame.reset 1u ErrorCode.NO_ERROR).SetName("RST_STREAM")
        TestCaseData(firstFrame, Frame.settings List.Empty).SetName("SETTINGS")
        TestCaseData(firstFrame, Frame.pingDefault ()).SetName("PING")
        TestCaseData(firstFrame, Frame.goAway 1u ErrorCode.NO_ERROR).SetName("GOAWAY")
        TestCaseData(firstFrame, Frame.windowUpdate 1u 100u).SetName("WINDOW_UPDATE")
        TestCaseData(firstFrame, Frame.pushPromise 1u).SetName("PUSH_PROMISE")
        TestCaseData(firstFrame, Frame.continuation 3u headers2).SetName("CONTINUATION with wrong stream id")
    }

[<TestCaseSource(nameof failedTransitionTestCases)>]
let testTransitionFails firstFrame secondFrame =
    let struct (pendingCnx, _) = ServerConnection.transition zeroCnx firstFrame

    ServerConnection.transition pendingCnx secondFrame
    |> shouldEqual (Transition.connectionError ErrorCode.PROTOCOL_ERROR pendingCnx)

[<Test>]
let testTransitionSucceeds () =
    let alphaFields1 =
        [ { Name = ":method"; Value = "GET" }
          { Name = ":scheme"; Value = "https" }
          { Name = ":path"; Value = "/resources" }
          { Name = ":authority"; Value = "example.com" } ]
        |> List.map FieldPack.Default

    let alphaFields2 =
        [ { Name = "user-agent"; Value = "Mozilla/5.0 (Windows NT 10.0; Win64; x64)" } ]
        |> List.map FieldPack.Default

    let betaFields =
        [ { Name = ":method"; Value = "POST" }
          { Name = ":scheme"; Value = "https" }
          { Name = ":path"; Value = "/api" }
          { Name = ":authority"; Value = "example.com" }
          { Name = "content-type"; Value = "application/json" }
          { Name = "content-length"; Value = "156" }
          { Name = "user-agent"; Value = "Mozilla/5.0 (Windows NT 10.0; Win64; x64)" } ]
        |> List.map FieldPack.Default

    let alphaBytes1, table1 = Table.encodeFieldsMem zeroTbl alphaFields1
    let alphaBytes2, table2 = Table.encodeFieldsMem table1 alphaFields2
    let betaBytes, table3 = Table.encodeFieldsMem table2 betaFields

    let cnx1 =
        { ServerConnection.Empty with
            NextStreamId = 3u
            HPackTable = table1
            ActiveStreams = Map.ofList [ (1u, H2Stream.Open 1u) ] }
        |> ConnectionState.withPendingHeader 1u (SizedBuffer.From alphaBytes1)

    let cnx2 = { cnx1 with HPackTable = table2; PendingFields = ValueNone }

    let cnx3 =
        { cnx2 with
            NextStreamId = 5u
            ActiveStreams = cnx1.ActiveStreams.Add(3u, H2Stream.Open 3u) }
        |> ConnectionState.withPendingHeader 3u (SizedBuffer.From betaBytes)

    Frame.headers 1u alphaBytes1
    |> ServerConnection.transition zeroCnx
    |> shouldEqual (Transition.pending cnx1)

    Frame.continuation 1u ReadOnlyMemory.Empty
    |> ServerConnection.transition cnx1
    |> shouldEqual (Transition.pending cnx1)

    Frame.continuation 1u alphaBytes2
    |> Frame.withFlags ContinuationFlags.END_HEADERS
    |> ServerConnection.transition cnx1
    |> shouldEqual (Transition.headerFields (H2Stream.Open 1u) (alphaFields1 @ alphaFields2) cnx2)

    Frame.headers 3u betaBytes
    |> ServerConnection.transition cnx2
    |> shouldEqual (Transition.pending cnx3)

    Frame.continuation 3u ReadOnlyMemory.Empty
    |> Frame.withFlags ContinuationFlags.END_HEADERS
    |> ServerConnection.transition cnx3
    |> shouldEqual (
        Transition.headerFields
            (H2Stream.Open 3u)
            betaFields
            { cnx3 with PendingFields = ValueNone; HPackTable = table3 }
    )

[<Test>]
let testReset () =
    let fields =
        [ { Name = ":method"; Value = "GET" }
          { Name = ":scheme"; Value = "https" }
          { Name = ":path"; Value = "/resources" }
          { Name = ":authority"; Value = "example.com" } ]
        |> List.map FieldPack.Default

    let headerBytes, table = Table.encodeFieldsMem zeroTbl fields

    let cnx1 =
        { ServerConnection.Empty with
            NextStreamId = 3u
            HPackTable = table
            ActiveStreams = Map.ofList [ (1u, H2Stream.Open 1u) ] }

    let cnx2 =
        { cnx1 with
            NextStreamId = 5u
            ActiveStreams = cnx1.ActiveStreams.Add(3u, H2Stream.Open 3u) }
        |> ConnectionState.withPendingHeader 3u (SizedBuffer.From headerBytes)

    let cnx3 =
        { cnx2 with
            ActiveStreams = Map.ofList [ (3u, H2Stream.Open 3u) ]
            ResetStreams = Set.singleton 1u }

    let cnx4 = { cnx3 with PendingFields = ValueNone; HPackTable = table }

    let cnx5 =
        { cnx4 with ActiveStreams = Map.empty; ResetStreams = cnx4.ResetStreams.Add 3u }

    Frame.headers 1u headerBytes
    |> Frame.withFlags ContinuationFlags.END_HEADERS
    |> ServerConnection.transition zeroCnx
    |> shouldEqual (Transition.headerFields (H2Stream.Open 1u) fields cnx1)

    Frame.headers 3u headerBytes
    |> ServerConnection.transition cnx1
    |> shouldEqual (Transition.pending cnx2)

    Frame.reset 1u ErrorCode.CANCEL
    |> ServerConnection.transition cnx2
    |> shouldEqual (Transition.streamClosed 1u cnx3)

    Frame.continuation 3u ReadOnlyMemory.Empty
    |> Frame.withFlags ContinuationFlags.END_HEADERS
    |> ServerConnection.transition cnx3
    |> shouldEqual (Transition.headerFields (H2Stream.Open 3u) fields cnx4)

    Frame.reset 3u ErrorCode.CANCEL
    |> ServerConnection.transition cnx4
    |> shouldEqual (Transition.streamClosed 3u cnx5)

// Test that CONTINUATION frames are accepted on HalfClosed streams (RFC 9113 §5.1)
// This covers the h2spec generic/3.10 fix: CONTINUATION is "effectively part of the HEADERS"
// and must be accepted regardless of whether END_STREAM was set on the initial HEADERS.
[<Test>]
let testContinuationOnHalfClosedStream () =
    let fields1 =
        [ { Name = ":method"; Value = "GET" }
          { Name = ":scheme"; Value = "https" }
          { Name = ":path"; Value = "/resources" }
          { Name = ":authority"; Value = "example.com" } ]
        |> List.map FieldPack.Default

    let fields2 =
        [ { Name = "user-agent"; Value = "Mozilla/5.0" } ] |> List.map FieldPack.Default

    let bytes1, table1 = Table.encodeFieldsMem zeroTbl fields1
    let bytes2, table2 = Table.encodeFieldsMem table1 fields2

    // Simulate a connection where the initial HEADERS had END_STREAM set,
    // transitioning the stream to HalfClosed, but END_HEADERS was not set,
    // so we're waiting for CONTINUATION frames.
    let cnx =
        { zeroCnx with
            NextStreamId = 3u
            ActiveStreams = Map.ofList [ (1u, H2Stream.HalfClosed 1u) ] }
        |> ConnectionState.withPendingHeader 1u (SizedBuffer.From bytes1)

    let expectedCnx = { cnx with PendingFields = ValueNone; HPackTable = table2 }

    Frame.continuation 1u bytes2
    |> Frame.withFlags ContinuationFlags.END_HEADERS
    |> ServerConnection.transition cnx
    |> shouldEqual (Transition.headerFields (H2Stream.HalfClosed 1u) (fields1 @ fields2) expectedCnx)

// Test multiple CONTINUATION frames on a HalfClosed stream (h2spec generic/3.10 case 2)
[<Test>]
let testMultipleContinuationsOnHalfClosedStream () =
    let fields1 =
        [ { Name = ":method"; Value = "GET" }
          { Name = ":scheme"; Value = "https" }
          { Name = ":path"; Value = "/resources" }
          { Name = ":authority"; Value = "example.com" } ]
        |> List.map FieldPack.Default

    let fields2 =
        [ { Name = "user-agent"; Value = "Mozilla/5.0" } ] |> List.map FieldPack.Default

    let fields3 =
        [ { Name = "accept"; Value = "text/html" } ] |> List.map FieldPack.Default

    let bytes1, table1 = Table.encodeFieldsMem zeroTbl fields1
    let bytes2, table2 = Table.encodeFieldsMem table1 fields2
    let bytes3, table3 = Table.encodeFieldsMem table2 fields3

    // Stream is HalfClosed (END_STREAM set on initial HEADERS),
    // header block split across HEADERS + 2 CONTINUATION frames.
    let cnx =
        { zeroCnx with
            NextStreamId = 3u
            ActiveStreams = Map.ofList [ (1u, H2Stream.HalfClosed 1u) ] }
        |> ConnectionState.withPendingHeader 1u (SizedBuffer.From bytes1)

    let expectedCnx = { cnx with PendingFields = ValueNone; HPackTable = table3 }

    // First CONTINUATION without END_HEADERS accumulates into pending buffer
    let struct (cnxAfterFirst, _) =
        Frame.continuation 1u bytes2 |> ServerConnection.transition cnx

    // Second CONTINUATION with END_HEADERS completes the header block
    Frame.continuation 1u bytes3
    |> Frame.withFlags ContinuationFlags.END_HEADERS
    |> ServerConnection.transition cnxAfterFirst
    |> shouldEqual (Transition.headerFields (H2Stream.HalfClosed 1u) (fields1 @ fields2 @ fields3) expectedCnx)

// End-to-end test: HEADERS with END_STREAM but no END_HEADERS, then CONTINUATION completes
[<Test>]
let testEndToEndContinuationOnHalfClosedStream () =
    let fields1 =
        [ { Name = ":method"; Value = "GET" }
          { Name = ":scheme"; Value = "https" }
          { Name = ":path"; Value = "/resources" }
          { Name = ":authority"; Value = "example.com" } ]
        |> List.map FieldPack.Default

    let fields2 =
        [ { Name = "user-agent"; Value = "Mozilla/5.0" } ] |> List.map FieldPack.Default

    let bytes1, table1 = Table.encodeFieldsMem zeroTbl fields1
    let bytes2, table2 = Table.encodeFieldsMem table1 fields2

    // HEADERS with END_STREAM (no END_HEADERS) opens stream in HalfClosed state
    let cnx1 =
        { zeroCnx with
            NextStreamId = 3u
            ActiveStreams = Map.ofList [ (1u, H2Stream.HalfClosed 1u) ] }
        |> ConnectionState.withPendingHeader 1u (SizedBuffer.From bytes1)

    let expectedCnx = { cnx1 with PendingFields = ValueNone; HPackTable = table2 }

    Frame.headers 1u bytes1
    |> Frame.withFlags HeadersFlags.END_STREAM
    |> ServerConnection.transition zeroCnx
    |> shouldEqual (Transition.pending cnx1)

    // CONTINUATION with END_HEADERS completes the header block
    Frame.continuation 1u bytes2
    |> Frame.withFlags ContinuationFlags.END_HEADERS
    |> ServerConnection.transition cnx1
    |> shouldEqual (Transition.headerFields (H2Stream.HalfClosed 1u) (fields1 @ fields2) expectedCnx)

// Test trailer fields split across HEADERS + CONTINUATION on an Open stream
[<Test>]
let testTrailerContinuationOnOpenStream () =
    let headerFields =
        [ { Name = ":method"; Value = "POST" }
          { Name = ":scheme"; Value = "https" }
          { Name = ":path"; Value = "/api" }
          { Name = ":authority"; Value = "example.com" } ]
        |> List.map FieldPack.Default

    let trailerFields1 =
        [ { Name = "x-trailer-1"; Value = "value1" } ] |> List.map FieldPack.Default

    let trailerFields2 =
        [ { Name = "x-trailer-2"; Value = "value2" } ] |> List.map FieldPack.Default

    let headerBytes, table1 = Table.encodeFieldsMem zeroTbl headerFields
    let trailerBytes1, table2 = Table.encodeFieldsMem table1 trailerFields1
    let trailerBytes2, table3 = Table.encodeFieldsMem table2 trailerFields2

    // Pre-establish stream 1 as Open, then send HEADERS without END_HEADERS
    let openCnx =
        { zeroCnx with
            NextStreamId = 3u
            HPackTable = table1
            ActiveStreams = Map.ofList [ (1u, H2Stream.Open 1u) ] }

    // HEADERS without END_HEADERS on Open stream creates pending trailer
    let cnx =
        openCnx
        |> ConnectionState.withPendingTrailer 1u (SizedBuffer.From trailerBytes1)

    let expectedCnx = { cnx with PendingFields = ValueNone; HPackTable = table3 }

    Frame.headers 1u trailerBytes1
    |> ServerConnection.transition openCnx
    |> shouldEqual (Transition.pending cnx)

    // CONTINUATION with END_HEADERS completes the trailer block
    Frame.continuation 1u trailerBytes2
    |> Frame.withFlags ContinuationFlags.END_HEADERS
    |> ServerConnection.transition cnx
    |> shouldEqual (Transition.trailerFields (H2Stream.Open 1u) (trailerFields1 @ trailerFields2) expectedCnx)

// Test multiple CONTINUATION frames for trailers on an Open stream
[<Test>]
let testMultipleTrailerContinuationsOnOpenStream () =
    let headerFields =
        [ { Name = ":method"; Value = "GET" }
          { Name = ":scheme"; Value = "https" }
          { Name = ":path"; Value = "/resources" }
          { Name = ":authority"; Value = "example.com" } ]
        |> List.map FieldPack.Default

    let trailerFields1 =
        [ { Name = "x-trailer-1"; Value = "a" } ] |> List.map FieldPack.Default

    let trailerFields2 =
        [ { Name = "x-trailer-2"; Value = "b" } ] |> List.map FieldPack.Default

    let trailerFields3 =
        [ { Name = "x-trailer-3"; Value = "c" } ] |> List.map FieldPack.Default

    let headerBytes, table1 = Table.encodeFieldsMem zeroTbl headerFields
    let trailerBytes1, table2 = Table.encodeFieldsMem table1 trailerFields1
    let trailerBytes2, table3 = Table.encodeFieldsMem table2 trailerFields2
    let trailerBytes3, table4 = Table.encodeFieldsMem table3 trailerFields3

    // Stream is Open, trailer block split across HEADERS + 2 CONTINUATION frames
    let cnx =
        { zeroCnx with
            NextStreamId = 3u
            HPackTable = table1
            ActiveStreams = Map.ofList [ (1u, H2Stream.Open 1u) ] }
        |> ConnectionState.withPendingTrailer 1u (SizedBuffer.From trailerBytes1)

    let expectedCnx = { cnx with PendingFields = ValueNone; HPackTable = table4 }

    // First CONTINUATION without END_HEADERS accumulates
    let struct (cnxAfterFirst, _) =
        Frame.continuation 1u trailerBytes2 |> ServerConnection.transition cnx

    // Second CONTINUATION with END_HEADERS completes the trailer block
    Frame.continuation 1u trailerBytes3
    |> Frame.withFlags ContinuationFlags.END_HEADERS
    |> ServerConnection.transition cnxAfterFirst
    |> shouldEqual (
        Transition.trailerFields (H2Stream.Open 1u) (trailerFields1 @ trailerFields2 @ trailerFields3) expectedCnx
    )

// Test trailer continuation on HalfClosed stream (END_STREAM set on trailer HEADERS)
[<Test>]
let testTrailerContinuationOnHalfClosedStream () =
    let headerFields =
        [ { Name = ":method"; Value = "GET" }
          { Name = ":scheme"; Value = "https" }
          { Name = ":path"; Value = "/resources" }
          { Name = ":authority"; Value = "example.com" } ]
        |> List.map FieldPack.Default

    let trailerFields1 =
        [ { Name = "grpc-status"; Value = "0" } ] |> List.map FieldPack.Default

    let trailerFields2 =
        [ { Name = "grpc-message"; Value = "OK" } ] |> List.map FieldPack.Default

    let headerBytes, table1 = Table.encodeFieldsMem zeroTbl headerFields
    let trailerBytes1, table2 = Table.encodeFieldsMem table1 trailerFields1
    let trailerBytes2, table3 = Table.encodeFieldsMem table2 trailerFields2

    // Stream is HalfClosed (END_STREAM set on trailer HEADERS), waiting for CONTINUATION
    let cnx =
        { zeroCnx with
            NextStreamId = 3u
            HPackTable = table1
            ActiveStreams = Map.ofList [ (1u, H2Stream.HalfClosed 1u) ] }
        |> ConnectionState.withPendingTrailer 1u (SizedBuffer.From trailerBytes1)

    let expectedCnx = { cnx with PendingFields = ValueNone; HPackTable = table3 }

    Frame.continuation 1u trailerBytes2
    |> Frame.withFlags ContinuationFlags.END_HEADERS
    |> ServerConnection.transition cnx
    |> shouldEqual (Transition.trailerFields (H2Stream.HalfClosed 1u) (trailerFields1 @ trailerFields2) expectedCnx)
