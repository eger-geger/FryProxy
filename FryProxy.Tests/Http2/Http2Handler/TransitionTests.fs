module FryProxy.Tests.Pipeline.Http2.TransitionTests

open System
open System.Threading.Channels
open FryProxy.Http2
open FryProxy.Http2.Frames
open FryProxy.IO
open FryProxy.Pipeline.Http2Handler
open FsUnit
open NUnit.Framework

let private closedStreams streamIds =
    let channel = Channel.CreateUnbounded<StreamId>()

    for streamId in streamIds do
        channel.Writer.TryWrite streamId |> ignore

    channel.Reader

[<Test>]
let ``ignores unsupported frames`` () =
    let state = ServerConnection.Empty
    let transition = transitionConnectionState <| closedStreams []
    let data = ReadOnlyMemory([| 1uy; 2uy; 3uy; 4uy; 5uy; 6uy; 7uy; 8uy |])

    let extFrame =
        { Header =
            { Length = 0u
              Type = LanguagePrimitives.EnumOfValue<byte, FrameType> 0xFFuy
              Flags = 0uy
              StreamId = 0u }
          Body = Extension { ExtensionData = ByteBuffer.empty } }

    transition state extFrame |> should equal
    <| Transition.discard ByteBuffer.empty state

    transition state (Frame.ping data) |> should equal <| Transition.ping data state

[<Test>]
let ``closes local and remote streams`` () =
    let transition = transitionConnectionState <| closedStreams [ 1u; 3u; 1u; 9u ]

    let state0 =
        { ServerConnection.Empty with
            NextStreamId = 5u
            ActiveStreams = Map.ofList [ 1u, H2Stream.Open 1u; 3u, H2Stream.Open 3u ]
            OpenedStreams = [ (1u, 3u) ] }

    let state1 =
        { state0 with ActiveStreams = Map.ofList [ 3u, H2Stream.HalfClosedLocal 3u ] }


    transition state0 (Frame.emptyData 1u |> Frame.withFlags DataFlags.END_STREAM)
    |> should equal
    <| Transition.content (H2Stream.Closed 1u) ByteBuffer.empty state1
