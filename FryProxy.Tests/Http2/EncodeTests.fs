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
    let tc name (expectedBytes: byte list) (frame: Frame) =
        TestCaseData(frame, List.toArray expectedBytes).SetName(name)

    [ tc "HEADERS END_STREAM|END_HEADERS streamId=1"
      <| [ 0x00uy; 0x00uy; 0x09uy; 0x01uy; 0x05uy; 0x00uy; 0x00uy; 0x00uy; 0x01uy ]
         @ [ 0x82uy; 0x84uy; 0x87uy; 0x41uy; 0x86uy; 0xA0uy; 0xE4uy; 0x1Duy; 0x13uy ]
      <| { Header =
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

      tc "HEADERS PADDED|END_HEADERS streamId=5"
      <| [ 0x00uy; 0x00uy; 0x05uy; 0x01uy; 0x0Cuy; 0x00uy; 0x00uy; 0x00uy; 0x05uy ]
         @ [ 0x02uy; 0x82uy; 0x84uy; 0x00uy; 0x00uy ]
      <| { Header =
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

      tc "HEADERS PRIORITY|END_HEADERS streamId=1 dep=3 weight=15"
      <| [ 0x00uy; 0x00uy; 0x07uy; 0x01uy; 0x24uy; 0x00uy; 0x00uy; 0x00uy; 0x01uy ]
         @ [ 0x00uy; 0x00uy; 0x00uy; 0x03uy; 0x0Fuy; 0x82uy; 0x84uy ]
      <| { Header =
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

      tc "HEADERS PRIORITY|PADDED|END_HEADERS streamId=3 exclusive dep=5 weight=31 pad=2"
      <| [ 0x00uy; 0x00uy; 0x0Auy; 0x01uy; 0x2Cuy; 0x00uy; 0x00uy; 0x00uy; 0x03uy ]
         @ [ 0x02uy; 0x80uy; 0x00uy; 0x00uy; 0x05uy; 0x1Fuy; 0x82uy; 0x84uy ]
         @ [ 0x00uy; 0x00uy ]
      <| { Header =
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

      tc "DATA unpadded streamId=5 payload=Hello"
      <| [ 0x00uy; 0x00uy; 0x05uy; 0x00uy; 0x00uy; 0x00uy; 0x00uy; 0x00uy; 0x05uy ]
         @ [ 0x48uy; 0x65uy; 0x6Cuy; 0x6Cuy; 0x6Fuy ]
      <| { Header = { Length = 5u; Flags = 0uy; Type = FrameType.DATA; StreamId = 5u }
           Body =
             Data
                 { PadLength = 0uy
                   Data = MemoryByteSeq(ReadOnlyMemory([| 0x48uy; 0x65uy; 0x6Cuy; 0x6Cuy; 0x6Fuy |])) } }

      tc "DATA PADDED streamId=9 payload=Hi pad=3"
      <| [ 0x00uy; 0x00uy; 0x06uy; 0x00uy; 0x08uy; 0x00uy; 0x00uy; 0x00uy; 0x09uy ]
         @ [ 0x03uy; 0x48uy; 0x69uy; 0x00uy; 0x00uy; 0x00uy ]
      <| { Header =
             { Length = 6u
               Flags = uint8 DataFlags.PADDED
               Type = FrameType.DATA
               StreamId = 9u }
           Body = Data { PadLength = 3uy; Data = MemoryByteSeq(ReadOnlyMemory([| 0x48uy; 0x69uy |])) } }

      tc "PRIORITY streamId=2 exclusive dependency=5 weight=31"
      <| [ 0x00uy; 0x00uy; 0x05uy; 0x02uy; 0x00uy; 0x00uy; 0x00uy; 0x00uy; 0x02uy ]
         @ [ 0x80uy; 0x00uy; 0x00uy; 0x05uy; 0x1Fuy ]
      <| { Header = { Length = 5u; Flags = 0uy; Type = FrameType.PRIORITY; StreamId = 2u }
           Body = Priority { Exclusive = true; Dependency = 5u; Weight = 31uy } }

      tc "RST_STREAM streamId=1 errorCode=PROTOCOL_ERROR"
      <| [ 0x00uy; 0x00uy; 0x04uy; 0x03uy; 0x00uy; 0x00uy; 0x00uy; 0x00uy; 0x01uy ]
         @ [ 0x00uy; 0x00uy; 0x00uy; 0x01uy ]
      <| { Header = { Length = 4u; Flags = 0uy; Type = FrameType.RST_STREAM; StreamId = 1u }
           Body = Reset { ErrorCode = ErrorCode.PROTOCOL_ERROR } }

      tc "RST_STREAM streamId=3 errorCode=CANCEL"
      <| [ 0x00uy; 0x00uy; 0x04uy; 0x03uy; 0x00uy; 0x00uy; 0x00uy; 0x00uy; 0x03uy ]
         @ [ 0x00uy; 0x00uy; 0x00uy; 0x08uy ]
      <| { Header = { Length = 4u; Flags = 0uy; Type = FrameType.RST_STREAM; StreamId = 3u }
           Body = Reset { ErrorCode = ErrorCode.CANCEL } }

      tc "RST_STREAM streamId=5 errorCode=FLOW_CONTROL_ERROR"
      <| [ 0x00uy; 0x00uy; 0x04uy; 0x03uy; 0x00uy; 0x00uy; 0x00uy; 0x00uy; 0x05uy ]
         @ [ 0x00uy; 0x00uy; 0x00uy; 0x03uy ]
      <| { Header = { Length = 4u; Flags = 0uy; Type = FrameType.RST_STREAM; StreamId = 5u }
           Body = Reset { ErrorCode = ErrorCode.FLOW_CONTROL_ERROR } }

      tc "RST_STREAM streamId=7 errorCode=REFUSED_STREAM"
      <| [ 0x00uy; 0x00uy; 0x04uy; 0x03uy; 0x00uy; 0x00uy; 0x00uy; 0x00uy; 0x07uy ]
         @ [ 0x00uy; 0x00uy; 0x00uy; 0x07uy ]
      <| { Header = { Length = 4u; Flags = 0uy; Type = FrameType.RST_STREAM; StreamId = 7u }
           Body = Reset { ErrorCode = ErrorCode.REFUSED_STREAM } }

      tc "RST_STREAM streamId=2147483647 errorCode=INTERNAL_ERROR"
      <| [ 0x00uy; 0x00uy; 0x04uy; 0x03uy; 0x00uy; 0x7Fuy; 0xFFuy; 0xFFuy; 0xFFuy ]
         @ [ 0x00uy; 0x00uy; 0x00uy; 0x02uy ]
      <| { Header =
             { Length = 4u
               Flags = 0uy
               Type = FrameType.RST_STREAM
               StreamId = 2147483647u }
           Body = Reset { ErrorCode = ErrorCode.INTERNAL_ERROR } }

      tc "SETTINGS ACK streamId=0"
      <| [ 0x00uy; 0x00uy; 0x00uy; 0x04uy; 0x01uy; 0x00uy; 0x00uy; 0x00uy; 0x00uy ]
      <| { Header =
             { Length = 0u
               Flags = uint8 SettingsFlags.ACK
               Type = FrameType.SETTINGS
               StreamId = 0u }
           Body = Settings { Settings = [] } }

      tc "SETTINGS streamId=0 with parameters"
      <| [ 0x00uy; 0x00uy; 0x0Cuy; 0x04uy; 0x00uy; 0x00uy; 0x00uy; 0x00uy; 0x00uy ]
         @ [ 0x00uy; 0x04uy; 0x00uy; 0x00uy; 0xFFuy; 0xFFuy ]
         @ [ 0x00uy; 0x05uy; 0x00uy; 0x00uy; 0x40uy; 0x00uy ]
      <| { Header = { Length = 12u; Flags = 0uy; Type = FrameType.SETTINGS; StreamId = 0u }
           Body =
             Settings
                 { Settings =
                     [ { Type = SettingType.INITIAL_WINDOW_SIZE; Value = 65535u }
                       { Type = SettingType.MAX_FRAME_SIZE; Value = 16384u } ] } }

      tc "PUSH_PROMISE PADDED|END_HEADERS streamId=3 promised=4"
      <| [ 0x00uy; 0x00uy; 0x08uy; 0x05uy; 0x0Cuy; 0x00uy; 0x00uy; 0x00uy; 0x03uy ]
         @ [ 0x02uy; 0x00uy; 0x00uy; 0x00uy; 0x04uy; 0x82uy; 0x00uy; 0x00uy ]
      <| { Header =
             { Type = FrameType.PUSH_PROMISE
               Flags = uint8 (PushPromiseFlags.PADDED ||| PushPromiseFlags.END_HEADERS)
               StreamId = 3u
               Length = 8u }
           Body = PushPromise { PadLength = 2uy; Promised = 4u; FieldBlock = ReadOnlyMemory([| 0x82uy |]) } }

      tc "PING streamId=0"
      <| [ 0x00uy; 0x00uy; 0x08uy; 0x06uy; 0x00uy; 0x00uy; 0x00uy; 0x00uy; 0x00uy ]
         @ [ 0x01uy; 0x02uy; 0x03uy; 0x04uy; 0x05uy; 0x06uy; 0x07uy; 0x08uy ]
      <| { Header = { Length = 8u; Flags = 0uy; Type = FrameType.PING; StreamId = 0u }
           Body = Ping { Data = ReadOnlyMemory([| 0x01uy; 0x02uy; 0x03uy; 0x04uy; 0x05uy; 0x06uy; 0x07uy; 0x08uy |]) } }

      tc "GOAWAY streamId=0 with debug data"
      <| [ 0x00uy; 0x00uy; 0x0Auy; 0x07uy; 0x00uy; 0x00uy; 0x00uy; 0x00uy; 0x00uy ]
         @ [ 0x00uy; 0x00uy; 0x00uy; 0x05uy; 0x00uy; 0x00uy; 0x00uy; 0x01uy ]
         @ [ 0xDEuy; 0xADuy ]
      <| { Header = { Length = 10u; Flags = 0uy; Type = FrameType.GOAWAY; StreamId = 0u }
           Body =
             GoAway
                 { Last = 5u
                   ErrorCode = ErrorCode.PROTOCOL_ERROR
                   DebugData = MemoryByteSeq(ReadOnlyMemory([| 0xDEuy; 0xADuy |])) } }

      tc "WINDOW_UPDATE streamId=0 increment=0"
      <| [ 0x00uy; 0x00uy; 0x04uy; 0x08uy; 0x00uy; 0x00uy; 0x00uy; 0x00uy; 0x00uy ]
         @ [ 0x00uy; 0x00uy; 0x00uy; 0x00uy ]
      <| { Header = { Length = 4u; Flags = 0uy; Type = FrameType.WINDOW_UPDATE; StreamId = 0u }
           Body = WindowUpdate { Increment = 0u } }

      tc "WINDOW_UPDATE streamId=0 increment=1000"
      <| [ 0x00uy; 0x00uy; 0x04uy; 0x08uy; 0x00uy; 0x00uy; 0x00uy; 0x00uy; 0x00uy ]
         @ [ 0x00uy; 0x00uy; 0x03uy; 0xE8uy ]
      <| { Header = { Length = 4u; Flags = 0uy; Type = FrameType.WINDOW_UPDATE; StreamId = 0u }
           Body = WindowUpdate { Increment = 1000u } }

      tc "WINDOW_UPDATE streamId=1 increment=65535"
      <| [ 0x00uy; 0x00uy; 0x04uy; 0x08uy; 0x00uy; 0x00uy; 0x00uy; 0x00uy; 0x01uy ]
         @ [ 0x00uy; 0x00uy; 0xFFuy; 0xFFuy ]
      <| { Header = { Length = 4u; Flags = 0uy; Type = FrameType.WINDOW_UPDATE; StreamId = 1u }
           Body = WindowUpdate { Increment = 65535u } }

      tc "WINDOW_UPDATE streamId=3 increment=2147483647"
      <| [ 0x00uy; 0x00uy; 0x04uy; 0x08uy; 0x00uy; 0x00uy; 0x00uy; 0x00uy; 0x03uy ]
         @ [ 0x7Fuy; 0xFFuy; 0xFFuy; 0xFFuy ]
      <| { Header = { Length = 4u; Flags = 0uy; Type = FrameType.WINDOW_UPDATE; StreamId = 3u }
           Body = WindowUpdate { Increment = 2147483647u } }

      tc "WINDOW_UPDATE streamId=2147483647 increment=1"
      <| [ 0x00uy; 0x00uy; 0x04uy; 0x08uy; 0x00uy; 0x7Fuy; 0xFFuy; 0xFFuy; 0xFFuy ]
         @ [ 0x00uy; 0x00uy; 0x00uy; 0x01uy ]
      <| { Header =
             { Length = 4u
               Flags = 0uy
               Type = FrameType.WINDOW_UPDATE
               StreamId = 2147483647u }
           Body = WindowUpdate { Increment = 1u } }

      tc "CONTINUATION END_HEADERS streamId=1"
      <| [ 0x00uy; 0x00uy; 0x02uy; 0x09uy; 0x04uy; 0x00uy; 0x00uy; 0x00uy; 0x01uy ]
         @ [ 0x82uy; 0x84uy ]
      <| { Header =
             { Length = 2u
               Flags = uint8 ContinuationFlags.END_HEADERS
               Type = FrameType.CONTINUATION
               StreamId = 1u }
           Body = Continuation { FieldFragment = ReadOnlyMemory([| 0x82uy; 0x84uy |]) } }

      tc "GOAWAY last=0 error=NO_ERROR"
      <| [ 0x00uy; 0x00uy; 0x08uy; 0x07uy; 0x00uy; 0x00uy; 0x00uy; 0x00uy; 0x00uy ]
         @ [ 0x00uy; 0x00uy; 0x00uy; 0x00uy; 0x00uy; 0x00uy; 0x00uy; 0x00uy ]
      <| { Header = { Length = 8u; Flags = 0uy; Type = FrameType.GOAWAY; StreamId = 0u }
           Body = GoAway { Last = 0u; ErrorCode = ErrorCode.NO_ERROR; DebugData = ByteBuffer.empty } }

      tc "GOAWAY last=100 error=INTERNAL_ERROR"
      <| [ 0x00uy; 0x00uy; 0x08uy; 0x07uy; 0x00uy; 0x00uy; 0x00uy; 0x00uy; 0x00uy ]
         @ [ 0x00uy; 0x00uy; 0x00uy; 0x64uy; 0x00uy; 0x00uy; 0x00uy; 0x02uy ]
      <| { Header = { Length = 8u; Flags = 0uy; Type = FrameType.GOAWAY; StreamId = 0u }
           Body =
             GoAway
                 { Last = 100u
                   ErrorCode = ErrorCode.INTERNAL_ERROR
                   DebugData = ByteBuffer.empty } }

      tc "GOAWAY last=1048575 error=FLOW_CONTROL_ERROR"
      <| [ 0x00uy; 0x00uy; 0x08uy; 0x07uy; 0x00uy; 0x00uy; 0x00uy; 0x00uy; 0x00uy ]
         @ [ 0x00uy; 0x0Fuy; 0xFFuy; 0xFFuy; 0x00uy; 0x00uy; 0x00uy; 0x03uy ]
      <| { Header = { Length = 8u; Flags = 0uy; Type = FrameType.GOAWAY; StreamId = 0u }
           Body =
             GoAway
                 { Last = 1048575u
                   ErrorCode = ErrorCode.FLOW_CONTROL_ERROR
                   DebugData = ByteBuffer.empty } }

      tc "GOAWAY last=2147483647 error=REFUSED_STREAM"
      <| [ 0x00uy; 0x00uy; 0x08uy; 0x07uy; 0x00uy; 0x00uy; 0x00uy; 0x00uy; 0x00uy ]
         @ [ 0x7Fuy; 0xFFuy; 0xFFuy; 0xFFuy; 0x00uy; 0x00uy; 0x00uy; 0x07uy ]
      <| { Header = { Length = 8u; Flags = 0uy; Type = FrameType.GOAWAY; StreamId = 0u }
           Body =
             GoAway
                 { Last = 2147483647u
                   ErrorCode = ErrorCode.REFUSED_STREAM
                   DebugData = ByteBuffer.empty } } ]

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

        let! parsedFrame = Parser.runS (Parse.frame FrameSize.Max) stream
        parsedFrame |> ValueOption.map _.Header |> should equal (ValueSome frame.Header)
    }

let frameSettingsCases =
    let tc name (settings: Setting list) (expected: Frame) =
        TestCaseData(settings, expected).SetName(name)

    let twoSettings =
        [ { Type = SettingType.INITIAL_WINDOW_SIZE; Value = 65535u }
          { Type = SettingType.MAX_FRAME_SIZE; Value = 16384u } ]

    [ tc "settings empty list" ([]: Setting list)
      <| { Header = { Length = 0u; Flags = 0uy; Type = FrameType.SETTINGS; StreamId = 0u }
           Body = Settings { Settings = [] } }

      tc "settings two parameters" twoSettings
      <| { Header = { Length = 12u; Flags = 0uy; Type = FrameType.SETTINGS; StreamId = 0u }
           Body = Settings { Settings = twoSettings } } ]

[<TestCaseSource(nameof frameSettingsCases)>]
let testFrameSettings (settings: Setting list) (expected: Frame) =
    Frame.settings settings |> should equal expected

let frameGoAwayCases =
    let emptyBody last errorCode =
        GoAway { Last = last; ErrorCode = errorCode; DebugData = ByteBuffer.empty }

    let tc name (last: uint32) (errorCode: ErrorCode) (expected: Frame) =
        TestCaseData(last, errorCode, expected).SetName(name)

    [ tc "goAway last=0 error=NO_ERROR" 0u ErrorCode.NO_ERROR
      <| { Header = { Length = 8u; Flags = 0uy; Type = FrameType.GOAWAY; StreamId = 0u }
           Body = emptyBody 0u ErrorCode.NO_ERROR }

      tc "goAway last=5 error=PROTOCOL_ERROR" 5u ErrorCode.PROTOCOL_ERROR
      <| { Header = { Length = 8u; Flags = 0uy; Type = FrameType.GOAWAY; StreamId = 0u }
           Body = emptyBody 5u ErrorCode.PROTOCOL_ERROR }

      tc "goAway last=100 error=INTERNAL_ERROR" 100u ErrorCode.INTERNAL_ERROR
      <| { Header = { Length = 8u; Flags = 0uy; Type = FrameType.GOAWAY; StreamId = 0u }
           Body = emptyBody 100u ErrorCode.INTERNAL_ERROR }

      tc "goAway last=1048575 error=FLOW_CONTROL_ERROR" 1048575u ErrorCode.FLOW_CONTROL_ERROR
      <| { Header = { Length = 8u; Flags = 0uy; Type = FrameType.GOAWAY; StreamId = 0u }
           Body = emptyBody 1048575u ErrorCode.FLOW_CONTROL_ERROR }

      tc "goAway max stream id error=REFUSED_STREAM" 2147483647u ErrorCode.REFUSED_STREAM
      <| { Header = { Length = 8u; Flags = 0uy; Type = FrameType.GOAWAY; StreamId = 0u }
           Body = emptyBody 2147483647u ErrorCode.REFUSED_STREAM } ]

[<TestCaseSource(nameof frameGoAwayCases)>]
let testFrameGoAway (last: uint32) (errorCode: ErrorCode) (expected: Frame) =
    Frame.goAway last errorCode |> should equal expected
