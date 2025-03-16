module FryProxy.Tests.Http2.ConnectionTests

open FryProxy.Http2
open FryProxy.Http2.Frames
open FryProxy.IO
open FsUnit
open NUnit.Framework


[<Test>]
let testIdleStreamFailure () =
    let frame =
        { Header = { Length = 0u; Flags = 0uy; Type = FrameType.DATA; StreamId = 2u }
          Body = Data { PadLength = 0uy; Data = ByteBuffer.empty } }

    frame
    |> Http2Connection.transition Http2Connection.Empty
    |> should equal (Http2Connection.error ErrorCode.PROTOCOL_ERROR)
