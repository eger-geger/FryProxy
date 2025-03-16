namespace FryProxy.Http2.Frames

open FryProxy.Http2

/// Used to initiate shutdown of a connection or to signal serious error conditions. Allows an endpoint to gracefully
/// stop accepting new streams while still finishing processing of previously established streams.
/// This enables administrative actions, like server maintenance.
[<Struct>]
type GoAwayBody =
    {
        /// Identifier of the last stream accepted for processing.
        Last: StreamId
        /// Reason for closing the connection.
        ErrorCode: ErrorCode
        /// Opaque diagnostic data with no predefined semantic.
        DebugData: Octets
    }