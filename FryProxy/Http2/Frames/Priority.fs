namespace FryProxy.Http2.Frames

open FryProxy.Http2

/// Deprecated frame type preserved for interoperability.
[<Struct>]
type PriorityBody = { Exclusive: bool; Dependency: StreamId; Weight: uint8 }