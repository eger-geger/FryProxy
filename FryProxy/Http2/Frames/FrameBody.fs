namespace FryProxy.Http2.Frames

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
