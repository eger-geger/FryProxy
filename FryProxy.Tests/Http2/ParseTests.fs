module FryProxy.Tests.Http2.ParseTests

open System
open System.IO
open FryProxy.Http2
open FryProxy.Http2.Frames
open FryProxy.IO.BufferedParser
open FryProxy.Tests.Constraints
open NUnit.Framework
open FsUnit

let frameParserSuccessCases =
    let succeed name (bytes: byte[][]) expected =
        TestCaseData(Array.concat bytes).Returns(expected).SetName(name)

    seq {
        succeed
            "HEADERS END_STREAM|END_HEADERS streamId=1"
            [| [| 0x00uy; 0x00uy; 0x09uy; 0x01uy; 0x05uy; 0x00uy; 0x00uy; 0x00uy |]
               [| 0x01uy; 0x82uy; 0x84uy; 0x87uy; 0x41uy; 0x86uy; 0xA0uy; 0xE4uy |]
               [| 0x1Duy; 0x13uy |] |]
            { Header =
                { Type = FrameType.HEADERS
                  Flags = uint8 (HeadersFlags.END_STREAM ||| HeadersFlags.END_HEADERS)
                  StreamId = 1u
                  Length = 9u }
              Body =
                Headers
                    { Dependency = 0u
                      Exclusive = false
                      PadLength = 0uy
                      Weight = 0uy
                      FieldFragment =
                        ReadOnlyMemory([| 0x82uy; 0x84uy; 0x87uy; 0x41uy; 0x86uy; 0xA0uy; 0xE4uy; 0x1Duy; 0x13uy |]) } }

        succeed
            "HEADERS END_HEADERS streamId=2"
            [| [| 0x00uy; 0x00uy; 0x05uy; 0x01uy; 0x04uy; 0x00uy; 0x00uy; 0x00uy |]
               [| 0x02uy; 0x82uy; 0x84uy; 0x86uy; 0x41uy; 0x8Fuy |] |]
            { Header =
                { Type = FrameType.HEADERS
                  Flags = uint8 HeadersFlags.END_HEADERS
                  StreamId = 2u
                  Length = 5u }
              Body =
                Headers
                    { Dependency = 0u
                      Exclusive = false
                      PadLength = 0uy
                      Weight = 0uy
                      FieldFragment = ReadOnlyMemory([| 0x82uy; 0x84uy; 0x86uy; 0x41uy; 0x8Fuy |]) } }

        succeed
            "HEADERS END_STREAM|END_HEADERS streamId=3 empty"
            [| [| 0x00uy; 0x00uy; 0x00uy; 0x01uy; 0x05uy; 0x00uy; 0x00uy; 0x00uy; 0x03uy |] |]
            { Header =
                { Type = FrameType.HEADERS
                  Flags = uint8 (HeadersFlags.END_STREAM ||| HeadersFlags.END_HEADERS)
                  StreamId = 3u
                  Length = 0u }
              Body =
                Headers
                    { Dependency = 0u
                      Exclusive = false
                      PadLength = 0uy
                      Weight = 0uy
                      FieldFragment = ReadOnlyMemory() } }

        succeed
            "HEADERS PADDED|END_HEADERS streamId=5 fragment=0x8284 pad=2"
            [| [| 0x00uy; 0x00uy; 0x05uy; 0x01uy; 0x0Cuy; 0x00uy; 0x00uy; 0x00uy; 0x05uy |]
               [| 0x02uy; 0x82uy; 0x84uy; 0x00uy; 0x00uy |] |]
            { Header =
                { Type = FrameType.HEADERS
                  Flags = uint8 (HeadersFlags.PADDED ||| HeadersFlags.END_HEADERS)
                  StreamId = 5u
                  Length = 5u }
              Body =
                Headers
                    { Dependency = 0u
                      Exclusive = false
                      PadLength = 2uy
                      Weight = 0uy
                      FieldFragment = ReadOnlyMemory([| 0x82uy; 0x84uy; 0x00uy; 0x00uy |]) } }

    }

[<TestCaseSource(nameof frameParserSuccessCases)>]
let testParseFrame (bytes: byte[]) =
    task {
        use buffer = new MemoryStream(bytes, false)
        return! Parser.runS Parse.frame buffer
    }

let testParseDataFrameCases =
    let succeed name (bytes: byte[][]) header padLen body =
        TestCaseData(Array.concat bytes, header, padLen, body).SetName(name)

    seq {
        succeed
            "DATA no flags streamId=5 payload=Hello"
            [| [| 0x00uy; 0x00uy; 0x05uy; 0x00uy; 0x00uy; 0x00uy; 0x00uy; 0x00uy; 0x05uy |]
               [| 0x48uy; 0x65uy; 0x6Cuy; 0x6Cuy; 0x6Fuy |] |]
            { Length = 5u; Type = FrameType.DATA; Flags = 0uy; StreamId = 5u }
            0uy
            [| 0x48uy; 0x65uy; 0x6Cuy; 0x6Cuy; 0x6Fuy |]

        succeed
            "DATA END_STREAM streamId=7 payload=0xAABBCC"
            [| [| 0x00uy; 0x00uy; 0x03uy; 0x00uy; 0x01uy; 0x00uy; 0x00uy; 0x00uy; 0x07uy |]
               [| 0xAAuy; 0xBBuy; 0xCCuy |] |]
            { Length = 3u
              Type = FrameType.DATA
              Flags = uint8 DataFlags.END_STREAM
              StreamId = 7u }
            0uy
            [| 0xAAuy; 0xBBuy; 0xCCuy |]

        succeed
            "DATA PADDED streamId=9 payload=Hi pad=3"
            [| [| 0x00uy; 0x00uy; 0x06uy; 0x00uy; 0x08uy; 0x00uy; 0x00uy; 0x00uy; 0x09uy |]
               [| 0x03uy; 0x48uy; 0x69uy; 0x00uy; 0x00uy; 0x00uy |] |]
            { Length = 6u
              Type = FrameType.DATA
              Flags = uint8 DataFlags.PADDED
              StreamId = 9u }
            3uy
            [| 0x48uy; 0x69uy; 0x00uy; 0x00uy; 0x00uy |]
    }

[<TestCaseSource(nameof testParseDataFrameCases)>]
let testParseDataFrame bytes header padLen body =
    task {
        use inputBuffer = new MemoryStream(bytes, false)
        use outputBuffer = new MemoryStream()
        let! frame = Parser.runS Parse.frame inputBuffer

        frame.Header |> should equal header

        match frame.Body with
        | Data fb ->
            do! fb.Data.WriteAsync(outputBuffer)
            fb.PadLength |> should equal padLen
            outputBuffer.ToArray() |> should equal body
        | _ -> failwith "Expected DATA frame"
    }

let frameParserFailCases =
    let tc name (bytes: byte[][]) =
        TestCaseData(Array.concat bytes).SetName(name)

    seq {
        tc "invalid frame type 0x0A" [| [| 0x00uy; 0x00uy; 0x00uy; 0x0Auy; 0x00uy; 0x00uy; 0x00uy; 0x00uy; 0x01uy |] |]

        tc "invalid frame type 0xFF" [| [| 0x00uy; 0x00uy; 0x00uy; 0xFFuy; 0x00uy; 0x00uy; 0x00uy; 0x00uy; 0x01uy |] |]

        tc "truncated header 5 bytes" [| [| 0x00uy; 0x00uy; 0x09uy; 0x01uy; 0x05uy |] |]

        tc "empty input" [||]
    }

[<TestCaseSource(nameof frameParserFailCases)>]
let testParseFrameFails (bytes: byte[]) =
    use stream = new MemoryStream(bytes, false)
    Parser.runS Parse.frame stream |> shouldThrowAsync<ParseError>.From
