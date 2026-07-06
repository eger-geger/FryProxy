namespace FryProxy.Http2

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
