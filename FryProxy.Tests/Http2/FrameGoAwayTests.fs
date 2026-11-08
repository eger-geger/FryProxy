module FryProxy.Tests.Http2.FrameGoAwayTests

open FryProxy.Http2
open FryProxy.Http2.Frames
open FryProxy.IO
open FsUnit
open NUnit.Framework

let goAwayCases =
    let tc name (last: uint32) (errorCode: ErrorCode) (expectedFrame: Frame) =
        TestCaseData(last, errorCode, expectedFrame).SetName(name)

    let emptyBody last errorCode =
        GoAway { Last = last; ErrorCode = errorCode; DebugData = ByteBuffer.empty }

    [ tc "last=0 error=NO_ERROR" 0u ErrorCode.NO_ERROR
      <| { Header = { Length = 8u; Flags = 0uy; Type = FrameType.GOAWAY; StreamId = 0u }
           Body = emptyBody 0u ErrorCode.NO_ERROR }

      tc "last=5 error=PROTOCOL_ERROR" 5u ErrorCode.PROTOCOL_ERROR
      <| { Header = { Length = 8u; Flags = 0uy; Type = FrameType.GOAWAY; StreamId = 0u }
           Body = emptyBody 5u ErrorCode.PROTOCOL_ERROR }

      tc "last=100 error=INTERNAL_ERROR" 100u ErrorCode.INTERNAL_ERROR
      <| { Header = { Length = 8u; Flags = 0uy; Type = FrameType.GOAWAY; StreamId = 0u }
           Body = emptyBody 100u ErrorCode.INTERNAL_ERROR }

      tc "last=1048575 error=FLOW_CONTROL_ERROR" 1048575u ErrorCode.FLOW_CONTROL_ERROR
      <| { Header = { Length = 8u; Flags = 0uy; Type = FrameType.GOAWAY; StreamId = 0u }
           Body = emptyBody 1048575u ErrorCode.FLOW_CONTROL_ERROR }

      tc "max stream id error=REFUSED_STREAM" 2147483647u ErrorCode.REFUSED_STREAM
      <| { Header = { Length = 8u; Flags = 0uy; Type = FrameType.GOAWAY; StreamId = 0u }
           Body = emptyBody 2147483647u ErrorCode.REFUSED_STREAM } ]

[<TestCaseSource(nameof goAwayCases)>]
let testGoAwayFrame (last: uint32) (errorCode: ErrorCode) (expectedFrame: Frame) =
    Frame.goAway last errorCode |> should equal expectedFrame
