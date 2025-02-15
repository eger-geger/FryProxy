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

type HttpStream = {
    Id: StreamId
    State: StreamState
}

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
    /// The endpoint refused the stream prior to performing any application processing.
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


[<Flags>]
type DataFlags =
    /// Indicates that the Pad Length field and any padding that it describes are present.
    | PADDED = 0x08uy
    ///  Indicates that this frame is the last that the endpoint will send for the identified stream.
    /// Setting this flag causes the stream to enter one of the "half-closed" states or the "closed" state.
    | END_STREAM = 0x01uy


/// Convey arbitrary, variable-length sequences of octets associated with a stream.
/// One or more DATA frames are used, for instance, to carry HTTP request or response message contents.
[<Struct>]
type DataFrame =
    {
        /// The length of the frame padding in units of octets.
        /// This field is conditional and is only present if the PADDED flag is set.
        PadLength: uint8
        /// Application data.
        /// The amount of data is the remainder of the frame payload after subtracting the length of the other fields that are present.
        Data: Octets
    }

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

///  Is used to open a stream, and additionally carries a field block fragment.
/// Despite the name, a HEADERS frame can carry a header section or a trailer section.
[<Struct>]
type HeadersFrame =
    {
        ///  Length of the frame padding in units of octets. This field is only present if the PADDED flag is set.
        PadLength: uint8
        /// This field is only present if the PRIORITY flag is set. Priority signals in HEADERS frames are deprecated.
        Exclusive: bool
        /// A 31-bit stream identifier. This field is only present if the PRIORITY flag is set.
        Dependency: StreamId
        /// This field is only present if the PRIORITY flag is set.
        Weight: uint8
        /// Field block.
        FieldBlock: byte ReadOnlyMemory
    }

/// Deprecated frame type preserved for interoperability.
[<Struct>]
type PriorityFrame = { Exclusive: bool; Dependency: StreamId; Weight: uint8 }

/// Allows for immediate termination of a stream.
/// Requests cancellation of a stream or to indicate that an error condition has occurred.
[<Struct>]
type ResetFrame =
    {
        /// Indicates why the stream is being terminated.
        ErrorCode: ErrorCode
    }

type SettingType =
    /// Maximum size of the compression table used to decode field blocks, in units of octets.
    /// The encoder can select any size equal to or less than this value by using signaling
    /// specific to the compression format inside a field block.
    | HEADER_TABLE_SIZE = 0x01us

    /// Enables or disables server push. A server must not send a PUSH_PROMISE frame if it receives this
    /// parameter set to a value of 0. A client that has both set this parameter to 0 and had it acknowledged
    /// MUST treat the receipt of a PUSH_PROMISE frame as a connection error of type PROTOCOL_ERROR.
    | ENABLE_PUSH = 0x02us

    /// Indicates the maximum number of concurrent streams that the sender will allow.
    /// This limit is directional: it applies to the number of streams that the sender permits the receiver to create.
    /// A value of 0 should not be treated as special by endpoints. A zero value does prevent the creation of new
    /// streams, however, this can also happen for any limit that is exhausted with active streams.
    | MAX_CONCURRENT_STREAMS = 0x03us

    /// Indicates the sender's initial window size (in units of octets) for stream-level flow control.
    /// Affects the window size of all streams. Values above the maximum flow-control window size MUST
    /// be treated as a connection error of type FLOW_CONTROL_ERROR.
    | INITIAL_WINDOW_SIZE = 0x04us

    /// Indicates the size of the largest frame payload that the sender is willing to receive, in units of octets.
    /// The value advertised by an endpoint MUST be between this initial value and the maximum allowed frame size
    /// (16 777 215 octets), inclusive. Values outside this range MUST be treated as a connection error of type
    /// PROTOCOL_ERROR.
    | MAX_FRAME_SIZE = 0x05us

    /// Advisory setting informs a peer of the maximum field section size that the sender is prepared to accept,
    /// in units of octets. The value is based on the uncompressed size of field lines, including the length of
    /// the name and value in units of octets plus an overhead of 32 octets for each field line.
    /// For any given request, a lower limit than what is advertised MAY be enforced.
    | MAX_HEADER_LIST_SIZE = 0x06us

/// Individual setting field contained in SETTINGS frame.
[<Struct>]
type Setting = Setting of SettingType * uint32

[<Flags>]
type SettingsFlags =
    /// Upon receiving a SETTINGS frame with the ACK flag set, the sender of the altered settings
    /// can rely on the values from the oldest unacknowledged SETTINGS frame having been applied.
    | ACK = 0x01uy

/// Conveys configuration parameters that affect how endpoints communicate, such as preferences and constraints on
/// peer behavior. Also used to acknowledge the receipt of those settings.
[<Struct>]
type SettingsFrame = { Settings: Setting List }

[<Flags>]
type PushPromiseFlags =
    /// Indicates that the Pad Length field and any padding that it describes are present.
    | PADDED = 0x08uy
    /// Indicates that this frame contains an entire field block and is not followed by any CONTINUATION frames.
    | END_HEADERS = 0x04uy

/// Notify the peer endpoint in advance of streams the sender intends to initiate.
[<Struct>]
type PushPromiseFrame =
    {
        /// A field containing the length of the frame padding in units of octets. Only present if the PADDED flag is set.
        PadLength: uint8

        /// Identifies the stream that is reserved by the PUSH_PROMISE.
        /// The promised stream identifier MUST be a valid choice for the next stream sent by the sender.
        Promised: StreamId

        /// A field block fragment containing the request control data and a header section.
        FieldBlock: Octets
    }

[<Flags>]
type PingFlags =
    /// Indicates that this PING frame is a PING response.
    | ACK = 0x01uy

/// Is a mechanism for measuring a minimal round-trip time from the sender, as well as determining whether an idle
// connection is still functional. Can be sent from any endpoint.
[<Struct>]
type PingFrame =
    {
        /// 8 octets of opaque data in the frame payload.
        /// A sender can include any value it chooses and use those octets in any fashion.
        OpaqueData: Octets
    }

/// Used to initiate shutdown of a connection or to signal serious error conditions. Allows an endpoint to gracefully
/// stop accepting new streams while still finishing processing of previously established streams.
/// This enables administrative actions, like server maintenance.
type GoAwayFrame =
    {
        /// Identifier of the last stream accepted for processing.
        Last: StreamId
        /// Reason for closing the connection.
        ErrorCode: ErrorCode
        /// Opaque diagnostic data with no predefined semantic.
        DebugData: Octets
    }

/// Used to implement flow control.
type WindowUpdate =
    {
        /// The number of octets that the sender can transmit in addition to the existing flow-control window.
        Increment: uint32
    }

type ContinuationFlags =
    /// Indicates that this frame ends a field block.
    | END_HEADERS = 0x04uy

/// Used to continue a sequence of field block fragments.
type ContinuationFrame = { FieldBlock: byte ReadOnlyMemory }
