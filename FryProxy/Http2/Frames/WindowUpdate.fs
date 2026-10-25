namespace FryProxy.Http2.Frames

open System.Diagnostics

/// Used to implement flow control.
[<Struct; StructuredFormatDisplay("{DisplayText}"); DebuggerDisplay("{DisplayText,nq}")>]
type WindowUpdateBody =
    {
        /// The number of octets that the sender can transmit in addition to the existing flow-control window.
        Increment: uint32
    }

    member this.DisplayText =
        $"{{ Increment = {this.Increment} }}"

    override this.ToString() = this.DisplayText
