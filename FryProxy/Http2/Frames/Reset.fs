namespace FryProxy.Http2.Frames

open FryProxy.Http2

/// Allows for immediate termination of a stream.
/// Requests cancellation of a stream or to indicate that an error condition has occurred.
[<Struct>]
type ResetBody =
    {
        /// Indicates why the stream is being terminated.
        ErrorCode: ErrorCode
    }
