module FryProxy.Tests.Http2.IdleTransitionTests

#nowarn "3391"

open System
open FryProxy.Extension
open FryProxy.Http
open FryProxy.Http2
open FryProxy.Http2.Frames
open FryProxy.Http2.Hpack
open NUnit.Framework

let pingBody = ReadOnlyMemory(Array.zeroCreate 8)

let transitionTestCases =
    let connWithOpenStream =
        { ServerConnection.Empty with
            NextStreamId = 3u
            ActiveStreams = Map.ofList [ (1u, H2Stream.Open 1u) ]
            OpenedStreams = [ (1u, 1u) ] }

    let connWithClosedStream =
        { ServerConnection.Empty with
            NextStreamId = 3u
            ActiveStreams = Map.ofList [ (1u, H2Stream.HalfClosedRemote 1u) ]
            OpenedStreams = [ (1u, 1u) ] }

    let fields =
        [ { Name = ":method"; Value = "GET" }
          { Name = ":scheme"; Value = "https" }
          { Name = ":path"; Value = "/resources" }
          { Name = ":authority"; Value = "example.com" } ]
        |> List.map FieldPack.Default

    let fieldBlock, table = Table.encodeFieldsMem Table.empty fields

    let settingsList =
        [ Setting.headerTableSize 4096u
          Setting.enablePush 1u
          Setting.maxConcurrentStreams 100u ]

    let protocolError =
        Transition.connectionError ErrorCode.PROTOCOL_ERROR ServerConnection.Empty

    let tc name (frame: Frame) transition =
        TestCaseData(frame).Returns(transition).SetName(name)

    [ tc "invalid frame type DATA" (Frame.emptyData 1u) protocolError
      tc "invalid frame type PUSH_PROMISE" (Frame.pushPromise 1u) protocolError
      tc "invalid frame type RST_STREAM" (Frame.reset 1u ErrorCode.NO_ERROR) protocolError
      tc "invalid frame type CONTINUATION" (Frame.continuation 1u ReadOnlyMemory.Empty) protocolError

      tc "empty ping frame"
      <| (Frame.ping ReadOnlyMemory.Empty)
      <| Transition.connectionError ErrorCode.FRAME_SIZE_ERROR ServerConnection.Empty

      tc "ping frame too long"
      <| (Frame.ping (ReadOnlyMemory(Array.zeroCreate 9)))
      <| Transition.connectionError ErrorCode.FRAME_SIZE_ERROR ServerConnection.Empty

      tc "ping request" (Frame.ping pingBody) (Transition.ping pingBody ServerConnection.Empty)
      tc "invalid ping stream Id" { Frame.ping pingBody with Header.StreamId = 1u } protocolError
      tc "ping ack" (Frame.ping pingBody |> Frame.withFlags PingFlags.ACK) (Transition.pending ServerConnection.Empty)

      tc "settings frame with non-zero stream Id"
      <| { Frame.settings List.Empty with Header.StreamId = 1u }
      <| protocolError

      tc "settings ack"
      <| (Frame.settings List.Empty |> Frame.withFlags SettingsFlags.ACK)
      <| Transition.settingsAck ServerConnection.Empty

      tc "settings ack with non-empty settings list"
      <| (Frame.settings settingsList |> Frame.withFlags SettingsFlags.ACK)
      <| Transition.connectionError ErrorCode.FRAME_SIZE_ERROR ServerConnection.Empty

      tc "client settings request" (Frame.settings settingsList)
      <| Transition.clientSettings settingsList ServerConnection.Empty

      tc "settings INITIAL_WINDOW_SIZE valid value"
      <| Frame.settings [ Setting.initialWindowSize 32768u ]
      <| struct ({ ServerConnection.Empty with InitialWindowSize = 32768 },
                 ClientSettings [ Setting.initialWindowSize 32768u ])

      tc "settings INITIAL_WINDOW_SIZE exceeds max window size"
      <| Frame.settings [ Setting.initialWindowSize (uint32 (Int32.MaxValue + 1)) ]
      <| Transition.connectionError ErrorCode.FLOW_CONTROL_ERROR ServerConnection.Empty

      tc "settings ENABLE_PUSH = 0"
      <| Frame.settings [ Setting.enablePush 0u ]
      <| Transition.clientSettings [ Setting.enablePush 0u ] ServerConnection.Empty

      tc "settings ENABLE_PUSH = 1"
      <| Frame.settings [ Setting.enablePush 1u ]
      <| Transition.clientSettings [ Setting.enablePush 1u ] ServerConnection.Empty

      tc "settings ENABLE_PUSH = 2 is protocol error"
      <| Frame.settings [ Setting.enablePush 2u ]
      <| protocolError

      tc "settings ENABLE_PUSH = 100 is protocol error"
      <| Frame.settings [ Setting.enablePush 100u ]
      <| protocolError

      tc "settings MAX_FRAME_SIZE below minimum (16383)"
      <| Frame.settings [ Setting.maxFrameSize 16383u ]
      <| protocolError

      tc "settings MAX_FRAME_SIZE at minimum (16384)"
      <| Frame.settings [ Setting.maxFrameSize 16384u ]
      <| Transition.clientSettings [ Setting.maxFrameSize 16384u ] ServerConnection.Empty

      tc "settings MAX_FRAME_SIZE at maximum (16777215)"
      <| Frame.settings [ Setting.maxFrameSize 16777215u ]
      <| Transition.clientSettings [ Setting.maxFrameSize 16777215u ] ServerConnection.Empty

      tc "settings MAX_FRAME_SIZE above maximum (16777216)"
      <| Frame.settings [ Setting.maxFrameSize 16777216u ]
      <| protocolError

      tc "settings INITIAL_WINDOW_SIZE = 1"
      <| Frame.settings [ Setting.initialWindowSize 1u ]
      <| struct ({ ServerConnection.Empty with InitialWindowSize = 1 }, ClientSettings [ Setting.initialWindowSize 1u ])

      tc "settings INITIAL_WINDOW_SIZE = 0"
      <| Frame.settings [ Setting.initialWindowSize 0u ]
      <| struct ({ ServerConnection.Empty with InitialWindowSize = 0 }, ClientSettings [ Setting.initialWindowSize 0u ])

      tc "settings INITIAL_WINDOW_SIZE = MaxValue"
      <| Frame.settings [ Setting.initialWindowSize (uint32 Int32.MaxValue) ]
      <| struct ({ ServerConnection.Empty with InitialWindowSize = Int32.MaxValue },
                 ClientSettings [ Setting.initialWindowSize (uint32 Int32.MaxValue) ])

      tc "connection window update with zero window size"
      <| Frame.windowUpdate 0u 0u
      <| protocolError

      tc "windows update on idle stream" (Frame.windowUpdate 1u 1u) <| protocolError

      tc "go away" (Frame.goAway 1u ErrorCode.NO_ERROR) (Transition.pending ServerConnection.Empty)

      tc "go away non-zero stream Id"
      <| { Frame.goAway 1u ErrorCode.NO_ERROR with Header.StreamId = 1u }
      <| protocolError

      tc "go away with error" (Frame.goAway 1u ErrorCode.COMPRESSION_ERROR)
      <| Transition.close ErrorCode.COMPRESSION_ERROR ServerConnection.Empty

      tc "empty headers frame" (Frame.headers 1u ReadOnlyMemory.Empty)
      <| (connWithOpenStream
          |> ConnectionState.withPendingHeader 1u SizedBuffer.Empty
          |> Transition.pending)

      tc "truncated field block"
      <| (Frame.headers 1u (fieldBlock.Slice(5))
          |> Frame.withFlags HeadersFlags.END_HEADERS)
      <| Transition.connectionError ErrorCode.COMPRESSION_ERROR connWithOpenStream

      tc "incomplete headers frame" (Frame.headers 1u fieldBlock)
      <| (connWithOpenStream
          |> ConnectionState.withPendingHeader 1u (SizedBuffer.From fieldBlock)
          |> Transition.pending)

      tc "complete headers frame"
      <| (Frame.headers 1u fieldBlock |> Frame.withFlags HeadersFlags.END_HEADERS)
      <| ({ connWithOpenStream with PendingFields = ValueNone; HPackTable = table }
          |> Transition.headerFields (H2Stream.Open 1u) fields)

      tc "complete headers frame closing stream"
      <| (Frame.headers 1u fieldBlock
          |> Frame.withFlags (HeadersFlags.END_HEADERS ||| HeadersFlags.END_STREAM))
      <| ({ connWithClosedStream with PendingFields = ValueNone; HPackTable = table }
          |> Transition.headerFields (H2Stream.HalfClosedRemote 1u) fields)

      tc "priority frame on stream id 0" (Frame.priority 0u) protocolError
      tc "priority frame on even stream id" (Frame.priority 2u) protocolError
      tc "empty priority frame" (Frame.priority 1u) (Transition.pending ServerConnection.Empty)

      tc "even stream id headers frame" (Frame.headers 2u ReadOnlyMemory.Empty) protocolError

      tc "stream id 0 data frame" (Frame.emptyData 0u) protocolError ]

[<TestCaseSource(nameof transitionTestCases)>]
let testTransition frame =
    frame |> ServerConnection.transition ServerConnection.Empty
