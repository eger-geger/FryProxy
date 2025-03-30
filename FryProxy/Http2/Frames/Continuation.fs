namespace FryProxy.Http2.Frames

open System

type ContinuationFlags =
    /// Indicates that this frame ends a field block.
    | END_HEADERS = 0x04uy

/// Used to continue a sequence of field block fragments.
[<Struct>]
type ContinuationBody = { FieldFragment: byte ReadOnlyMemory }
