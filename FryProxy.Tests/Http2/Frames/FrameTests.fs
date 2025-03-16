module FryProxy.Tests.Http2.Frames.FrameTests

open FryProxy.Http2
open FryProxy.Http2.Frames
open FsUnit
open NUnit.Framework

[<Test>]
let testFrameTypeSize () =
    sizeof<FrameBody> |> should equal 8
    sizeof<FrameHeader> |> should equal 12
    sizeof<Frame> |> should equal 24
