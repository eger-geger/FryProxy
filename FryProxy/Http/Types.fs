namespace FryProxy.Http

/// Simplified URI
type Resource = { Host: string; Port: int; AbsoluteRef: string }


/// Type of HTTP message body
type MessageBodyType =
    | Empty
    | Content of length: uint64
    | Chunked
