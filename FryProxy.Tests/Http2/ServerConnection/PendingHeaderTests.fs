module FryProxy.Tests.Http2.ServerConnection.PendingHeaderTests

#nowarn "3391"

open System
open FryProxy.Extension
open FryProxy.Http
open FryProxy.Http2
open FryProxy.Http2.Frames
open FryProxy.Http2.Hpack
open FsUnitTyped
open NUnit.Framework

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

    let struct (headers1, table) = firstFields |> Table.encodeFields Table.empty
    let struct (headers2, _) = secondFields |> Table.encodeFields table

    let firstFrame = Frame.headers 1u headers1

    seq {
        TestCaseData(firstFrame, Frame.headers 1u headers2).SetName("HEADERS")
        TestCaseData(firstFrame, Frame.emptyData 1u).SetName("DATA")
        TestCaseData(firstFrame, Frame.priority 1u).SetName("PRIORITY")
        TestCaseData(firstFrame, Frame.reset 1u ErrorCode.NO_ERROR).SetName("RST_STREAM")
        TestCaseData(firstFrame, Frame.settings 1u List.Empty).SetName("SETTINGS")
        TestCaseData(firstFrame, Frame.ping 1u).SetName("PING")
        TestCaseData(firstFrame, Frame.goAway 1u ErrorCode.NO_ERROR).SetName("GOAWAY")
        TestCaseData(firstFrame, Frame.windowUpdate 1u 100u).SetName("WINDOW_UPDATE")
        TestCaseData(firstFrame, Frame.pushPromise 1u).SetName("PUSH_PROMISE")
        TestCaseData(firstFrame, Frame.continuation 3u headers2).SetName("CONTINUATION with wrong stream id")
    }

[<TestCaseSource(nameof failedTransitionTestCases)>]
let testTransitionFails firstFrame secondFrame =
    let struct (_, pendingCnx) =
        ServerConnection.transition ServerConnection.Empty firstFrame
        |> Result.defaultWith (fun _ -> failwith "transition should succeed")

    ServerConnection.transition pendingCnx secondFrame
    |> shouldEqual (Transition.error ErrorCode.PROTOCOL_ERROR)

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

    let struct (alphaBytes1, table1) = alphaFields1 |> Table.encodeFields Table.empty
    let struct (alphaBytes2, table2) = alphaFields2 |> Table.encodeFields table1
    let struct (betaBytes, table3) = betaFields |> Table.encodeFields table2

    let cnx1 =
        { ServerConnection.Empty with
            NextStreamId = 3u
            HPackTable = table1
            Streams = [ { Id = 1u; State = StreamState.Open } ]
            PendingHeader = ValueSome { StreamId = 1u; Buffer = SizedBuffer.From alphaBytes1 } }

    let cnx2 = { cnx1 with HPackTable = table2; PendingHeader = ValueNone }

    let cnx3 =
        { cnx2 with
            NextStreamId = 5u
            HPackTable = table3
            Streams = { Id = 3u; State = StreamState.Open } :: cnx1.Streams
            PendingHeader = ValueSome { StreamId = 3u; Buffer = SizedBuffer.From betaBytes } }

    Frame.headers 1u alphaBytes1
    |> ServerConnection.transition ServerConnection.Empty
    |> shouldEqual (Transition.pending cnx1)

    Frame.continuation 1u ReadOnlyMemory.Empty
    |> ServerConnection.transition cnx1
    |> shouldEqual (Transition.pending cnx1)

    Frame.continuation 1u alphaBytes2
    |> Frame.withFlags ContinuationFlags.END_HEADERS
    |> ServerConnection.transition cnx1
    |> shouldEqual (Transition.fields (alphaFields1 @ alphaFields2) cnx2)

    Frame.headers 3u betaBytes
    |> ServerConnection.transition cnx2
    |> shouldEqual (Transition.pending cnx3)

    Frame.continuation 3u ReadOnlyMemory.Empty
    |> Frame.withFlags ContinuationFlags.END_HEADERS
    |> ServerConnection.transition cnx3
    |> shouldEqual (Transition.decodedFields betaFields table3 cnx3)
