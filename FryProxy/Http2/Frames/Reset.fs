namespace FryProxy.Http2.Frames

open System.Diagnostics
open FryProxy.Http2

/// Allows for immediate termination of a stream.
/// Requests cancellation of a stream or to indicate that an error condition has occurred.
[<Struct; StructuredFormatDisplay("{DisplayText}"); DebuggerDisplay("{DisplayText,nq}")>]
type ResetBody =
    {
        /// Indicates why the stream is being terminated.
        ErrorCode: ErrorCode
    }

    member this.DisplayText =
        $"{{ ErrorCode = {this.ErrorCode} }}"

    override this.ToString() = this.DisplayText
