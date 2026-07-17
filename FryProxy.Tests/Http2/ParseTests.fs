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

        succeed
            "PRIORITY streamId=1 dependency=3 weight=15"
            [| [| 0x00uy; 0x00uy; 0x05uy; 0x02uy; 0x00uy; 0x00uy; 0x00uy; 0x00uy; 0x01uy |]
               [| 0x00uy; 0x00uy; 0x00uy; 0x03uy; 0x0Fuy |] |]
            { Header = { Type = FrameType.PRIORITY; Flags = 0uy; StreamId = 1u; Length = 5u }
              Body = Priority { Exclusive = false; Dependency = 3u; Weight = 15uy } }

        succeed
            "PRIORITY streamId=2 exclusive dependency=5 weight=31"
            [| [| 0x00uy; 0x00uy; 0x05uy; 0x02uy; 0x00uy; 0x00uy; 0x00uy; 0x00uy; 0x02uy |]
               [| 0x80uy; 0x00uy; 0x00uy; 0x05uy; 0x1Fuy |] |]
            { Header = { Type = FrameType.PRIORITY; Flags = 0uy; StreamId = 2u; Length = 5u }
              Body = Priority { Exclusive = true; Dependency = 5u; Weight = 31uy } }

        succeed
            "RST_STREAM streamId=3 errorCode=CANCEL"
            [| [| 0x00uy; 0x00uy; 0x04uy; 0x03uy; 0x00uy; 0x00uy; 0x00uy; 0x00uy; 0x03uy |]
               [| 0x00uy; 0x00uy; 0x00uy; 0x08uy |] |]
            { Header = { Type = FrameType.RST_STREAM; Flags = 0uy; StreamId = 3u; Length = 4u }
              Body = Reset { ErrorCode = ErrorCode.CANCEL } }

        succeed
            "SETTINGS ACK streamId=0"
            [| [| 0x00uy; 0x00uy; 0x00uy; 0x04uy; 0x01uy; 0x00uy; 0x00uy; 0x00uy; 0x00uy |] |]
            { Header =
                { Type = FrameType.SETTINGS
                  Flags = uint8 SettingsFlags.ACK
                  StreamId = 0u
                  Length = 0u }
              Body = Settings { Settings = [] } }

        succeed
            "SETTINGS streamId=0 INITIAL_WINDOW_SIZE=65535 MAX_FRAME_SIZE=16384"
            [| [| 0x00uy; 0x00uy; 0x0Cuy; 0x04uy; 0x00uy; 0x00uy; 0x00uy; 0x00uy; 0x00uy |]
               [| 0x00uy; 0x04uy; 0x00uy; 0x00uy; 0xFFuy; 0xFFuy |]
               [| 0x00uy; 0x05uy; 0x00uy; 0x00uy; 0x40uy; 0x00uy |] |]
            { Header = { Type = FrameType.SETTINGS; Flags = 0uy; StreamId = 0u; Length = 12u }
              Body =
                Settings
                    { Settings =
                        [ Setting(SettingType.INITIAL_WINDOW_SIZE, 65535u)
                          Setting(SettingType.MAX_FRAME_SIZE, 16384u) ] } }

        succeed
            "WINDOW_UPDATE streamId=0 increment=1000"
            [| [| 0x00uy; 0x00uy; 0x04uy; 0x08uy; 0x00uy; 0x00uy; 0x00uy; 0x00uy; 0x00uy |]
               [| 0x00uy; 0x00uy; 0x03uy; 0xE8uy |] |]
            { Header = { Type = FrameType.WINDOW_UPDATE; Flags = 0uy; StreamId = 0u; Length = 4u }
              Body = WindowUpdate { Increment = 1000u } }

        succeed
            "WINDOW_UPDATE streamId=5 increment=500 reserved-bit-set"
            [| [| 0x00uy; 0x00uy; 0x04uy; 0x08uy; 0x00uy; 0x00uy; 0x00uy; 0x00uy; 0x05uy |]
               [| 0x80uy; 0x00uy; 0x01uy; 0xF4uy |] |]
            { Header = { Type = FrameType.WINDOW_UPDATE; Flags = 0uy; StreamId = 5u; Length = 4u }
              Body = WindowUpdate { Increment = 500u } }

        succeed
            "PING streamId=0 data=0x0102030405060708"
            [| [| 0x00uy; 0x00uy; 0x08uy; 0x06uy; 0x00uy; 0x00uy; 0x00uy; 0x00uy; 0x00uy |]
               [| 0x01uy; 0x02uy; 0x03uy; 0x04uy; 0x05uy; 0x06uy; 0x07uy; 0x08uy |] |]
            { Header = { Type = FrameType.PING; Flags = 0uy; StreamId = 0u; Length = 8u }
              Body =
                Ping { Data = ReadOnlyMemory([| 0x01uy; 0x02uy; 0x03uy; 0x04uy; 0x05uy; 0x06uy; 0x07uy; 0x08uy |]) } }

        succeed
            "PING ACK streamId=0 data=zeros"
            [| [| 0x00uy; 0x00uy; 0x08uy; 0x06uy; 0x01uy; 0x00uy; 0x00uy; 0x00uy; 0x00uy |]
               [| 0x00uy; 0x00uy; 0x00uy; 0x00uy; 0x00uy; 0x00uy; 0x00uy; 0x00uy |] |]
            { Header =
                { Type = FrameType.PING
                  Flags = uint8 PingFlags.ACK
                  StreamId = 0u
                  Length = 8u }
              Body =
                Ping { Data = ReadOnlyMemory([| 0x00uy; 0x00uy; 0x00uy; 0x00uy; 0x00uy; 0x00uy; 0x00uy; 0x00uy |]) } }

        succeed
            "PUSH_PROMISE END_HEADERS streamId=1 promised=2 fragment=0x8284"
            [| [| 0x00uy; 0x00uy; 0x06uy; 0x05uy; 0x04uy; 0x00uy; 0x00uy; 0x00uy; 0x01uy |]
               [| 0x00uy; 0x00uy; 0x00uy; 0x02uy; 0x82uy; 0x84uy |] |]
            { Header =
                { Type = FrameType.PUSH_PROMISE
                  Flags = uint8 PushPromiseFlags.END_HEADERS
                  StreamId = 1u
                  Length = 6u }
              Body =
                PushPromise
                    { PadLength = 0uy
                      Promised = 2u
                      FieldBlock = ReadOnlyMemory([| 0x82uy; 0x84uy |]) } }

        succeed
            "PUSH_PROMISE PADDED|END_HEADERS streamId=3 promised=4 fragment=0x82 pad=2"
            [| [| 0x00uy; 0x00uy; 0x08uy; 0x05uy; 0x0Cuy; 0x00uy; 0x00uy; 0x00uy; 0x03uy |]
               [| 0x02uy; 0x00uy; 0x00uy; 0x00uy; 0x04uy; 0x82uy; 0x00uy; 0x00uy |] |]
            { Header =
                { Type = FrameType.PUSH_PROMISE
                  Flags = uint8 (PushPromiseFlags.PADDED ||| PushPromiseFlags.END_HEADERS)
                  StreamId = 3u
                  Length = 8u }
              Body =
                PushPromise
                    { PadLength = 2uy
                      Promised = 4u
                      FieldBlock = ReadOnlyMemory([| 0x82uy; 0x00uy; 0x00uy |]) } }

        succeed
            "CONTINUATION END_HEADERS streamId=1 fragment=0x8284"
            [| [| 0x00uy; 0x00uy; 0x02uy; 0x09uy; 0x04uy; 0x00uy; 0x00uy; 0x00uy; 0x01uy |]
               [| 0x82uy; 0x84uy |] |]
            { Header =
                { Type = FrameType.CONTINUATION
                  Flags = uint8 ContinuationFlags.END_HEADERS
                  StreamId = 1u
                  Length = 2u }
              Body = Continuation { FieldFragment = ReadOnlyMemory([| 0x82uy; 0x84uy |]) } }

        succeed
            "CONTINUATION streamId=3 empty"
            [| [| 0x00uy; 0x00uy; 0x00uy; 0x09uy; 0x00uy; 0x00uy; 0x00uy; 0x00uy; 0x03uy |] |]
            { Header = { Type = FrameType.CONTINUATION; Flags = 0uy; StreamId = 3u; Length = 0u }
              Body = Continuation { FieldFragment = ReadOnlyMemory() } }

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

let testParseGoAwayFrameCases =
    let succeed name (bytes: byte[][]) header last errorCode debugData =
        TestCaseData(Array.concat bytes, header, last, errorCode, debugData).SetName(name)

    seq {
        succeed
            "GOAWAY streamId=0 last=3 errorCode=NO_ERROR"
            [| [| 0x00uy; 0x00uy; 0x08uy; 0x07uy; 0x00uy; 0x00uy; 0x00uy; 0x00uy; 0x00uy |]
               [| 0x00uy; 0x00uy; 0x00uy; 0x03uy; 0x00uy; 0x00uy; 0x00uy; 0x00uy |] |]
            { Type = FrameType.GOAWAY; Flags = 0uy; StreamId = 0u; Length = 8u }
            3u
            ErrorCode.NO_ERROR
            ([||]: byte[])

        succeed
            "GOAWAY streamId=0 last=5 errorCode=PROTOCOL_ERROR debugData=0xDEAD"
            [| [| 0x00uy; 0x00uy; 0x0Auy; 0x07uy; 0x00uy; 0x00uy; 0x00uy; 0x00uy; 0x00uy |]
               [| 0x00uy; 0x00uy; 0x00uy; 0x05uy; 0x00uy; 0x00uy; 0x00uy; 0x01uy; 0xDEuy |]
               [| 0xADuy |] |]
            { Type = FrameType.GOAWAY; Flags = 0uy; StreamId = 0u; Length = 10u }
            5u
            ErrorCode.PROTOCOL_ERROR
            [| 0xDEuy; 0xADuy |]
    }

[<TestCaseSource(nameof testParseGoAwayFrameCases)>]
let testParseGoAwayFrame (bytes: byte[]) header last errorCode (expectedDebug: byte[]) =
    task {
        use inputBuffer = new MemoryStream(bytes, false)
        use outputBuffer = new MemoryStream()
        let! frame = Parser.runS Parse.frame inputBuffer

        frame.Header |> should equal header

        match frame.Body with
        | GoAway ga ->
            ga.Last |> should equal last
            ga.ErrorCode |> should equal errorCode
            do! ga.DebugData.WriteAsync(outputBuffer)
            outputBuffer.ToArray() |> should equal expectedDebug
        | _ -> failwith "Expected GOAWAY frame"
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
