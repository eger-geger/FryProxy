module FryProxy.Tests.Pipeline.Http2.TransitionTests

open System
open System.Threading.Channels
open FryProxy.Http2
open FryProxy.Http2.Frames
open FryProxy.IO
open FryProxy.Pipeline.Http2Handler
open FsUnitTyped
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

    transition state ValueNone
    |> shouldEqual (ValueSome <| Transition.pending state)

    transition state (ValueSome <| Frame.ping data)
    |> shouldEqual (ValueSome <| Transition.ping data state)

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

    transition state0 (Frame.emptyData 1u |> Frame.withFlags DataFlags.END_STREAM |> ValueSome)
    |> shouldEqual (ValueSome <| Transition.content (H2Stream.Closed 1u) ByteBuffer.empty state1)
