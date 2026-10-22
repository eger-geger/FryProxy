module FryProxy.Tests.Http2.Http2WriterTests

open System
open System.Buffers
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
let ``run writes correct frames`` (messages: OutboundMessage list) (expectedBody: byte[]) =
    task {
        use stream = new MemoryStream()
        let destination = PipeWriter.Create(stream)
        let channel = Channel.CreateUnbounded<OutboundMessage>()

        for message in messages do
            do! channel.Writer.WriteAsync(message)

        channel.Writer.Complete()
        do! Http2Writer.run channel.Reader destination

        stream.Position <- 0L
        let! frames = Parser.runS (Parser.eager Parse.frame) stream

        frames
        |> List.map _.Header.Type
        |> should equal [ FrameType.HEADERS; FrameType.DATA ]

        frames.Head.Header.StreamId |> should equal 1u

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
