namespace FryProxy.Http2.Frames

open System
open FryProxy.IO
open FryProxy.Http2

type FrameBody =
    | Headers of HeadersBody
    | Data of DataBody
    | Priority of PriorityBody
    | Reset of ResetBody
    | Settings of SettingsBody
    | PushPromise of PushPromiseBody
    | Ping of PingBody
    | GoAway of GoAwayBody
    | WindowUpdate of WindowUpdateBody
    | Continuation of ContinuationBody

[<Struct>]
type Frame = { Header: FrameHeader; Body: FrameBody }

module Frame =
    let emptyData streamId =
        { Header = { Length = 0u; Flags = 0uy; Type = FrameType.DATA; StreamId = streamId }
          Body = Data { PadLength = 0uy; Data = ByteBuffer.empty } }

    let headers streamId (fieldFragment: byte ReadOnlyMemory) =
        { Header =
            { Length = uint32 fieldFragment.Length
              Flags = 0uy
              Type = FrameType.HEADERS
              StreamId = streamId }
          Body =
            Headers
                { PadLength = 0uy
                  Exclusive = false
                  Dependency = 0u
                  Weight = 0uy
                  FieldFragment = fieldFragment } }

    let priority streamId =
        { Header = { Length = 0u; Flags = 0uy; Type = FrameType.PRIORITY; StreamId = streamId }
          Body = Priority { Exclusive = false; Dependency = 0u; Weight = 0uy } }

    let reset id code =
        { Header = { Length = 0u; Flags = 0uy; Type = FrameType.RST_STREAM; StreamId = id }
          Body = Reset { ErrorCode = code } }

    let settings id settings =
        { Header = { Length = 0u; Flags = 0uy; Type = FrameType.SETTINGS; StreamId = id }
          Body = Settings { Settings = settings } }

    let pushPromise id =
        { Header = { Length = 0u; Flags = 0uy; Type = FrameType.PUSH_PROMISE; StreamId = id }
          Body = PushPromise { Promised = id; PadLength = 0uy; FieldBlock = ReadOnlyMemory.Empty } }

    let ping id =
        { Header = { Length = 0u; Flags = 0uy; Type = FrameType.PING; StreamId = id }
          Body = Ping { OpaqueData = ByteBuffer.empty } }

    let goAway id err =
        { Header = { Length = 0u; Flags = 0uy; Type = FrameType.GOAWAY; StreamId = id }
          Body = GoAway { Last = id; ErrorCode = err; DebugData = ByteBuffer.empty } }

    let windowUpdate id increment =
        { Header = { Length = 0u; Flags = 0uy; Type = FrameType.WINDOW_UPDATE; StreamId = id }
          Body = WindowUpdate { Increment = increment } }

    let continuation id =
        { Header = { Length = 0u; Flags = 0uy; Type = FrameType.CONTINUATION; StreamId = id }
          Body = Continuation { FieldFragment = ReadOnlyMemory.Empty } }

    let inline hasFlag flag (frame: Frame) = FrameHeader.hasFlag flag frame.Header

    let inline withFlags flags (frame: Frame) =
        { frame with Header.Flags = frame.Header.Flags ||| uint8 flags }
