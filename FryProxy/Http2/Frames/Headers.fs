namespace FryProxy.Http2.Frames

open System
open FryProxy.Http2

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
type HeadersBody =
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