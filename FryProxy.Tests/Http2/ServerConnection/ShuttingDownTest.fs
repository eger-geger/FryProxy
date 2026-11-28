module FryProxy.Tests.Http2.ServerConnection.ShuttingDownTest

open System
open FryProxy.IO
open FryProxy.Http2
open FryProxy.Http2.Frames
open NUnit.Framework

let shuttingDownCnx =
    { ServerConnection.Empty with
        LastStreamId = ValueSome 9u
        NextStreamId = 13u
        ActiveStreams = Map.ofList [ (1u, H2Stream.Open 1u); (11u, H2Stream.Open 11u) ]
        OpenedStreams = [ (1u, 1u); (11u, 11u) ] }

let transitionTestCases =
    let pingBody = ReadOnlyMemory([| 0uy; 1uy; 2uy; 3uy; 4uy; 5uy; 6uy; 7uy |])

    let tc name (frame: Frame) (expected: ConnectionStateTransition) =
        TestCaseData([| box frame |], ExpectedResult = expected).SetName(name)

    let pending name frame =
        tc name frame (Transition.pending shuttingDownCnx)

    [ pending "second go away" (Frame.goAway 1u ErrorCode.NO_ERROR)

      tc "second go away with error"
      <| Frame.goAway 1u ErrorCode.PROTOCOL_ERROR
      <| Transition.close ErrorCode.PROTOCOL_ERROR shuttingDownCnx

      tc "new stream with higher stream Id"
      <| Frame.headers 13u ReadOnlyMemory.Empty
      <| Transition.streamRefused 13u shuttingDownCnx

      tc "remaining headers"
      <| (Frame.headers 1u ReadOnlyMemory.Empty
          |> Frame.withFlags HeadersFlags.END_HEADERS)
      <| (Transition.streamError ErrorCode.PROTOCOL_ERROR 1u
          <| { shuttingDownCnx with
                 ActiveStreams = Map.remove 1u shuttingDownCnx.ActiveStreams })

      tc "remaining data"
      <| Frame.emptyData 1u
      <| Transition.content (H2Stream.Open 1u) (MemoryByteSeq()) shuttingDownCnx

      pending "remaining data on ignored stream" (Frame.emptyData 11u |> Frame.withFlags HeadersFlags.END_STREAM)

      pending "priority frame" (Frame.priority 1u)

      tc "ping frame"
      <| Frame.ping pingBody
      <| Transition.ping pingBody shuttingDownCnx

      pending "connection windows update" (Frame.windowUpdate 0u 10u)

      pending "stream windows update" (Frame.windowUpdate 1u 8u)

      pending "stream windows update on ignored stream" (Frame.windowUpdate 11u 8u) ]


[<TestCaseSource(nameof transitionTestCases)>]
let testTransition frame =
    ServerConnection.transition shuttingDownCnx frame
