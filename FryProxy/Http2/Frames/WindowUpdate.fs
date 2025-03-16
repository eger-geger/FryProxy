namespace FryProxy.Http2.Frames

/// Used to implement flow control.
[<Struct>]
type WindowUpdateBody =
    {
        /// The number of octets that the sender can transmit in addition to the existing flow-control window.
        Increment: uint32
    }
