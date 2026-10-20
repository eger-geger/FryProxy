module FryProxy.Tests.Http2.EncodeTests

open System
open System.IO
open System.IO.Pipelines
open FryProxy.IO
open FryProxy.IO.BufferedParser
open FryProxy.Http2
open FryProxy.Http2.Frames
open FsUnit
open NUnit.Framework

let frameEncodeCases =
    let tc name (expectedBytes: byte[]) (frame: Frame) =
        TestCaseData(frame, expectedBytes).SetName(name)

    [ tc
          "HEADERS END_STREAM|END_HEADERS streamId=1"
          [| 0x00uy; 0x00uy; 0x09uy; 0x01uy; 0x05uy; 0x00uy; 0x00uy; 0x00uy; 0x01uy
             0x82uy; 0x84uy; 0x87uy; 0x41uy; 0x86uy; 0xA0uy; 0xE4uy; 0x1Duy; 0x13uy |]
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

      tc
          "HEADERS PADDED|END_HEADERS streamId=5"
          [| 0x00uy; 0x00uy; 0x05uy; 0x01uy; 0x0Cuy; 0x00uy; 0x00uy; 0x00uy; 0x05uy
             0x02uy; 0x82uy; 0x84uy; 0x00uy; 0x00uy |]
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
                    FieldFragment = ReadOnlyMemory([| 0x82uy; 0x84uy |]) } }

      tc
          "HEADERS PRIORITY|END_HEADERS streamId=1 dep=3 weight=15"
          [| 0x00uy; 0x00uy; 0x07uy; 0x01uy; 0x24uy; 0x00uy; 0x00uy; 0x00uy; 0x01uy
             0x00uy; 0x00uy; 0x00uy; 0x03uy; 0x0Fuy
             0x82uy; 0x84uy |]
          { Header =
              { Type = FrameType.HEADERS
                Flags = uint8 (HeadersFlags.PRIORITY ||| HeadersFlags.END_HEADERS)
                StreamId = 1u
                Length = 7u }
            Body =
              Headers
                  { Dependency = 3u
                    Exclusive = false
                    PadLength = 0uy
                    Weight = 15uy
                    FieldFragment = ReadOnlyMemory([| 0x82uy; 0x84uy |]) } }

      tc
          "HEADERS PRIORITY|PADDED|END_HEADERS streamId=3 exclusive dep=5 weight=31 pad=2"
          [| 0x00uy; 0x00uy; 0x0Auy; 0x01uy; 0x2Cuy; 0x00uy; 0x00uy; 0x00uy; 0x03uy
             0x02uy
             0x80uy; 0x00uy; 0x00uy; 0x05uy; 0x1Fuy
             0x82uy; 0x84uy
             0x00uy; 0x00uy |]
          { Header =
              { Type = FrameType.HEADERS
                Flags = uint8 (HeadersFlags.PRIORITY ||| HeadersFlags.PADDED ||| HeadersFlags.END_HEADERS)
                StreamId = 3u
                Length = 10u }
            Body =
              Headers
                  { Dependency = 5u
                    Exclusive = true
                    PadLength = 2uy
                    Weight = 31uy
                    FieldFragment = ReadOnlyMemory([| 0x82uy; 0x84uy |]) } }

      tc
          "DATA unpadded streamId=5 payload=Hello"
          [| 0x00uy; 0x00uy; 0x05uy; 0x00uy; 0x00uy; 0x00uy; 0x00uy; 0x00uy; 0x05uy
             0x48uy; 0x65uy; 0x6Cuy; 0x6Cuy; 0x6Fuy |]
          { Header = { Length = 5u; Type = FrameType.DATA; Flags = 0uy; StreamId = 5u }
            Body = Data { PadLength = 0uy; Data = MemoryByteSeq(ReadOnlyMemory([| 0x48uy; 0x65uy; 0x6Cuy; 0x6Cuy; 0x6Fuy |])) } }

      tc
          "DATA PADDED streamId=9 payload=Hi pad=3"
          [| 0x00uy; 0x00uy; 0x06uy; 0x00uy; 0x08uy; 0x00uy; 0x00uy; 0x00uy; 0x09uy
             0x03uy; 0x48uy; 0x69uy; 0x00uy; 0x00uy; 0x00uy |]
          { Header = { Length = 6u; Type = FrameType.DATA; Flags = uint8 DataFlags.PADDED; StreamId = 9u }
            Body = Data { PadLength = 3uy; Data = MemoryByteSeq(ReadOnlyMemory([| 0x48uy; 0x69uy |])) } }

      tc
          "PRIORITY streamId=2 exclusive dependency=5 weight=31"
          [| 0x00uy; 0x00uy; 0x05uy; 0x02uy; 0x00uy; 0x00uy; 0x00uy; 0x00uy; 0x02uy
             0x80uy; 0x00uy; 0x00uy; 0x05uy; 0x1Fuy |]
          { Header = { Type = FrameType.PRIORITY; Flags = 0uy; StreamId = 2u; Length = 5u }
            Body = Priority { Exclusive = true; Dependency = 5u; Weight = 31uy } }

      tc
          "RST_STREAM streamId=3 errorCode=CANCEL"
          [| 0x00uy; 0x00uy; 0x04uy; 0x03uy; 0x00uy; 0x00uy; 0x00uy; 0x00uy; 0x03uy
             0x00uy; 0x00uy; 0x00uy; 0x08uy |]
          { Header = { Type = FrameType.RST_STREAM; Flags = 0uy; StreamId = 3u; Length = 4u }
            Body = Reset { ErrorCode = ErrorCode.CANCEL } }

      tc
          "SETTINGS ACK streamId=0"
          [| 0x00uy; 0x00uy; 0x00uy; 0x04uy; 0x01uy; 0x00uy; 0x00uy; 0x00uy; 0x00uy |]
          { Header =
              { Type = FrameType.SETTINGS
                Flags = uint8 SettingsFlags.ACK
                StreamId = 0u
                Length = 0u }
            Body = Settings { Settings = [] } }

      tc
          "SETTINGS streamId=0 with parameters"
          [| 0x00uy; 0x00uy; 0x0Cuy; 0x04uy; 0x00uy; 0x00uy; 0x00uy; 0x00uy; 0x00uy
             0x00uy; 0x04uy; 0x00uy; 0x00uy; 0xFFuy; 0xFFuy
             0x00uy; 0x05uy; 0x00uy; 0x00uy; 0x40uy; 0x00uy |]
          { Header = { Type = FrameType.SETTINGS; Flags = 0uy; StreamId = 0u; Length = 12u }
            Body =
              Settings
                  { Settings =
                      [ Setting(SettingType.INITIAL_WINDOW_SIZE, 65535u)
                        Setting(SettingType.MAX_FRAME_SIZE, 16384u) ] } }

      tc
          "PUSH_PROMISE PADDED|END_HEADERS streamId=3 promised=4"
          [| 0x00uy; 0x00uy; 0x08uy; 0x05uy; 0x0Cuy; 0x00uy; 0x00uy; 0x00uy; 0x03uy
             0x02uy; 0x00uy; 0x00uy; 0x00uy; 0x04uy; 0x82uy; 0x00uy; 0x00uy |]
          { Header =
              { Type = FrameType.PUSH_PROMISE
                Flags = uint8 (PushPromiseFlags.PADDED ||| PushPromiseFlags.END_HEADERS)
                StreamId = 3u
                Length = 8u }
            Body =
              PushPromise
                  { PadLength = 2uy
                    Promised = 4u
                    FieldBlock = ReadOnlyMemory([| 0x82uy |]) } }

      tc
          "PING streamId=0"
          [| 0x00uy; 0x00uy; 0x08uy; 0x06uy; 0x00uy; 0x00uy; 0x00uy; 0x00uy; 0x00uy
             0x01uy; 0x02uy; 0x03uy; 0x04uy; 0x05uy; 0x06uy; 0x07uy; 0x08uy |]
          { Header = { Type = FrameType.PING; Flags = 0uy; StreamId = 0u; Length = 8u }
            Body = Ping { Data = ReadOnlyMemory([| 0x01uy; 0x02uy; 0x03uy; 0x04uy; 0x05uy; 0x06uy; 0x07uy; 0x08uy |]) } }

      tc
          "GOAWAY streamId=0 with debug data"
          [| 0x00uy; 0x00uy; 0x0Auy; 0x07uy; 0x00uy; 0x00uy; 0x00uy; 0x00uy; 0x00uy
             0x00uy; 0x00uy; 0x00uy; 0x05uy; 0x00uy; 0x00uy; 0x00uy; 0x01uy; 0xDEuy; 0xADuy |]
          { Header = { Type = FrameType.GOAWAY; Flags = 0uy; StreamId = 0u; Length = 10u }
            Body =
              GoAway
                  { Last = 5u
                    ErrorCode = ErrorCode.PROTOCOL_ERROR
                    DebugData = MemoryByteSeq(ReadOnlyMemory([| 0xDEuy; 0xADuy |])) } }

      tc
          "WINDOW_UPDATE streamId=0 increment=1000"
          [| 0x00uy; 0x00uy; 0x04uy; 0x08uy; 0x00uy; 0x00uy; 0x00uy; 0x00uy; 0x00uy
             0x00uy; 0x00uy; 0x03uy; 0xE8uy |]
          { Header = { Type = FrameType.WINDOW_UPDATE; Flags = 0uy; StreamId = 0u; Length = 4u }
            Body = WindowUpdate { Increment = 1000u } }

      tc
          "CONTINUATION END_HEADERS streamId=1"
          [| 0x00uy; 0x00uy; 0x02uy; 0x09uy; 0x04uy; 0x00uy; 0x00uy; 0x00uy; 0x01uy
             0x82uy; 0x84uy |]
          { Header =
              { Type = FrameType.CONTINUATION
                Flags = uint8 ContinuationFlags.END_HEADERS
                StreamId = 1u
                Length = 2u }
            Body = Continuation { FieldFragment = ReadOnlyMemory([| 0x82uy; 0x84uy |]) } } ]

[<TestCaseSource(nameof frameEncodeCases)>]
let testEncodeFrame (frame: Frame) (expectedBytes: byte[]) =
    task {
        use stream = new MemoryStream()
        let writer = PipeWriter.Create stream
        let! _ = Encode.frame frame writer
        stream.ToArray() |> should equal expectedBytes
    }

[<TestCaseSource(nameof frameEncodeCases)>]
let testEncodeFrameRoundTrip (frame: Frame) (_: byte[]) =
    task {
        use stream = new MemoryStream()
        let writer = PipeWriter.Create stream
        let! _ = Encode.frame frame writer
        stream.Position <- 0L

        let! parsedFrame = Parser.runS Parse.frame stream
        parsedFrame.Header |> should equal frame.Header
    }
