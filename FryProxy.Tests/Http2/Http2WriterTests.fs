module FryProxy.Tests.Http2.Http2WriterTests

open System
open System.IO
open System.IO.Pipelines
open System.Threading.Channels
open FryProxy.Http
open FryProxy.Http2
open FryProxy.Http2.Frames
open FryProxy.Http2.Hpack
open FryProxy.IO.BufferedParser
open FsUnit
open NUnit.Framework


let private responseFields =
    [ { Field.Name = ":status"; Value = "200" }
      { Field.Name = "content-type"; Value = "text/plain" } ]

let private cases =
    seq {
        let emptyBody = ReadOnlyMemory.Empty
        let helloBody = ReadOnlyMemory(System.Text.Encoding.UTF8.GetBytes "hello")

        yield
            TestCaseData(
                [ OutboundMessage.Headers(1u, responseFields)
                  OutboundMessage.Frame(Frame.binaryData 1u emptyBody |> Frame.withFlags DataFlags.END_STREAM) ],
                emptyBody.ToArray()
            )
                .SetName("OK response without body")

        yield
            TestCaseData(
                [ OutboundMessage.Headers(1u, responseFields)
                  OutboundMessage.Frame(Frame.binaryData 1u helloBody |> Frame.withFlags DataFlags.END_STREAM) ],
                helloBody.ToArray()
            )
                .SetName("OK response with body")
    }

[<TestCaseSource(nameof cases)>]
let ``writes correct frames`` (messages: OutboundMessage list) (expectedBody: byte[]) =
    task {
        use stream = new MemoryStream()
        let destination = PipeWriter.Create(stream)
        let channel = Channel.CreateUnbounded<OutboundMessage>()
        let closedStreams = Channel.CreateUnbounded<StreamId>()

        for message in messages do
            do! channel.Writer.WriteAsync(message)

        channel.Writer.Complete()
        do! Http2Writer.run channel.Reader closedStreams.Writer destination

        closedStreams.Reader.TryRead() |> should equal (true, 1u)
        closedStreams.Reader.TryRead() |> fst |> should equal false

        stream.Position <- 0L
        let! rawFrames = Parser.runS (Parse.frame FrameSize.Max |> Parser.eager) stream
        let frames = rawFrames |> List.choose ValueOption.toOption

        frames
        |> List.map _.Header.Type
        |> should equal [ FrameType.HEADERS; FrameType.DATA ]

        frames.Head.Header.StreamId |> should equal 1u
        Frame.hasFlag HeadersFlags.END_STREAM frames.Head |> should equal false

        let headerFragment =
            match frames.Head.Body with
            | FrameBody.Headers headers -> headers.FieldFragment
            | _ -> failwith "Expected a HEADERS frame"

        let decodedFields =
            Table.decodeFields Table.empty headerFragment.Span
            |> Result.map (fun struct (fields, _) -> fields |> List.map FieldPack.field)

        decodedFields
        |> Result.defaultWith (fun error -> failwithf $"Could not decode headers: %A{error}")
        |> should equal responseFields

        match List.last frames with
        | { Body = FrameBody.Data data } ->
            use body = new MemoryStream()
            do! data.Data.WriteAsync(body)
            body.ToArray() |> should equal expectedBody
        | _ -> failwith "Expected a DATA frame"
    }

[<Test>]
let ``publishes only DATA and HEADERS with END_STREAM in order`` () =
    task {
        use stream = new MemoryStream()
        let destination = PipeWriter.Create(stream)
        let channel = Channel.CreateUnbounded<OutboundMessage>()
        let closedStreams = Channel.CreateUnbounded<StreamId>()

        let messages =
            [ OutboundMessage.Headers(9u, responseFields)
              OutboundMessage.Frame(Frame.emptyData 11u)
              OutboundMessage.Frame(
                  Frame.headers 13u ReadOnlyMemory.Empty
                  |> Frame.withFlags HeadersFlags.END_HEADERS
              )
              OutboundMessage.Frame(Frame.settings [] |> Frame.withFlags SettingsFlags.ACK)
              OutboundMessage.Frame(Frame.pingDefault () |> Frame.withFlags PingFlags.ACK)
              OutboundMessage.Frame(Frame.reset 15u ErrorCode.CANCEL |> Frame.withFlags FrameFlags.END_STREAM)
              OutboundMessage.Frame(Frame.emptyData 1u |> Frame.withFlags DataFlags.END_STREAM)
              OutboundMessage.Frame(
                  Frame.headers 3u ReadOnlyMemory.Empty
                  |> Frame.withFlags (HeadersFlags.END_STREAM ||| HeadersFlags.END_HEADERS)
              )
              OutboundMessage.Frame(Frame.emptyData 1u |> Frame.withFlags DataFlags.END_STREAM) ]

        for message in messages do
            do! channel.Writer.WriteAsync(message)

        channel.Writer.Complete()
        do! Http2Writer.run channel.Reader closedStreams.Writer destination

        closedStreams.Reader.TryRead() |> should equal (true, 1u)
        closedStreams.Reader.TryRead() |> should equal (true, 3u)
        closedStreams.Reader.TryRead() |> should equal (true, 1u)
        closedStreams.Reader.TryRead() |> fst |> should equal false
    }

[<Test>]
let ``writes END_STREAM frame when notification channel is completed`` () =
    task {
        use stream = new MemoryStream()
        let destination = PipeWriter.Create(stream)
        let channel = Channel.CreateUnbounded<OutboundMessage>()
        let closedStreams = Channel.CreateUnbounded<StreamId>()
        closedStreams.Writer.Complete()

        do! channel.Writer.WriteAsync(OutboundMessage.Frame(Frame.emptyData 1u |> Frame.withFlags DataFlags.END_STREAM))
        channel.Writer.Complete()
        do! Http2Writer.run channel.Reader closedStreams.Writer destination

        stream.Position <- 0L
        let! frames = Parser.runS (Parse.frame FrameSize.Max |> Parser.eager) stream

        frames
        |> List.choose ValueOption.toOption
        |> List.map _.Header
        |> should equal [ (Frame.emptyData 1u |> Frame.withFlags DataFlags.END_STREAM).Header ]
    }
