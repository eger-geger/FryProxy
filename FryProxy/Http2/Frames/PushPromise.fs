namespace FryProxy.Http2.Frames

open System
open FryProxy.Http2

[<Flags>]
type PushPromiseFlags =
    /// Indicates that the Pad Length field and any padding that it describes are present.
    | PADDED = 0x08uy
    /// Indicates that this frame contains an entire field block and is not followed by any CONTINUATION frames.
    | END_HEADERS = 0x04uy

/// Notify the peer endpoint in advance of streams the sender intends to initiate.
[<Struct>]
type PushPromiseBody =
    {
        /// A field containing the length of the frame padding in units of octets. Only present if the PADDED flag is set.
        PadLength: uint8

        /// Identifies the stream that is reserved by the PUSH_PROMISE.
        /// The promised stream identifier MUST be a valid choice for the next stream sent by the sender.
        Promised: StreamId

        /// A field block fragment containing the request control data and a header section.
        FieldBlock: byte ReadOnlyMemory
    }
