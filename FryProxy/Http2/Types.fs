namespace FryProxy.Http2

open System
open FryProxy.IO

type Octets = IByteBuffer

/// 31-bit integer identifying a stream.
/// Streams initiated by a client MUST use odd-numbered stream identifiers;
/// those initiated by the server MUST use even-numbered stream identifiers.
/// A stream identifier of zero (0x00) is used for connection control messages;
/// the stream identifier of zero cannot be used to establish a new stream.
type StreamId = uint32

[<Struct>]
type StreamState =
    | Idle
    | Open
    | Closed
    | Reserved
    | HalfClosed


type ErrorCode =
    /// The associated condition is not a result of an error.
    | NO_ERROR = 0x00u
    /// The endpoint detected an unspecific protocol error. Use when a more specific error code is not available.
    | PROTOCOL_ERROR = 0x01u
    /// The endpoint encountered an unexpected internal error.
    | INTERNAL_ERROR = 0x02u
    /// The endpoint detected that its peer violated the flow-control protocol.
    | FLOW_CONTROL_ERROR = 0x03u
    /// The endpoint sent a SETTINGS frame but did not receive a response in a timely manner.
    | SETTINGS_TIMEOUT = 0x04u
    /// The endpoint received a frame after a stream was half-closed.
    | STREAM_CLOSED = 0x05u
    /// The endpoint received a frame with an invalid size.
    | FRAME_SIZE_ERROR = 0x06u
    /// The endpoint refused the stream before performing any application processing.
    | REFUSED_STREAM = 0x07u
    /// The endpoint uses this error code to indicate that the stream is no longer needed.
    | CANCEL = 0x08u
    /// The endpoint is unable to maintain the field section compression context for the connection.
    | COMPRESSION_ERROR = 0x09u
    /// The connection established in response to a CONNECT request was reset or abnormally closed.
    | CONNECT_ERROR = 0x0au
    /// The endpoint detected that its peer is exhibiting a behavior that might be generating excessive load.
    | ENHANCE_YOUR_CALM = 0x0bu
    /// The underlying transport has properties that do not meet minimum security requirements.
    | INADEQUATE_SECURITY = 0x0cu
    /// The endpoint requires that HTTP/1.1 be used instead of HTTP/2.
    | HTTP_1_1_REQUIRED = 0x0du

type FrameType =
    | DATA = 0x00uy
    | HEADERS = 0x01uy
    | PRIORITY = 0x02uy
    | RST_STREAM = 0x03uy
    | SETTINGS = 0x04uy
    | PUSH_PROMISE = 0x05uy
    | PING = 0x06uy
    | GOAWAY = 0x07uy
    | WINDOW_UPDATE = 0x08uy
    | CONTINUATION = 0x09uy

module FrameFlags =

    [<Literal>]
    let END_STREAM = 0x01uy

    [<Literal>]
    let END_HEADERS = 0x04uy

[<Flags>]
type DataFlags =
    /// Indicates that the Pad Length field and any padding that it describes are present.
    | PADDED = 0x08uy
    /// Indicates that this frame is the last that the endpoint will send for the identified stream.
    /// Setting this flag causes the stream to enter one of the "half-closed" states or the "closed" state.
    | END_STREAM = 0x01uy

[<Flags>]
type HeadersFlags =
    /// Indicates that the Exclusive, Stream Dependency, and Weight fields are present.
    | PRIORITY = 0x20uy
    /// Indicates that the Pad Length field and any padding that it describes are present.
    | PADDED = 0x08uy
    /// Indicates that this frame contains an entire field block and is not followed by any CONTINUATION frames.
    | END_HEADERS = 0x04uy
    /// Indicates that the field block is the last that the endpoint will send for the identified stream.
    | END_STREAM = 0x01uy

[<Flags>]
type SettingsFlags =
    /// Upon receiving a SETTINGS frame with the ACK flag set, the sender of the altered settings
    /// can rely on the values from the oldest unacknowledged SETTINGS frame having been applied.
    | ACK = 0x01uy

[<Flags>]
type PingFlags =
    /// Indicates that this PING frame is a PING response.
    | ACK = 0x01uy

[<Flags>]
type PushPromiseFlags =
    /// Indicates that the Pad Length field and any padding that it describes are present.
    | PADDED = 0x08uy
    /// Indicates that this frame contains an entire field block and is not followed by any CONTINUATION frames.
    | END_HEADERS = 0x04uy

[<Flags>]
type ContinuationFlags =
    /// Indicates that this frame ends a field block.
    | END_HEADERS = 0x04uy
