namespace FryProxy.Http2.Frames

open System
open FryProxy.Http2

[<Flags>]
type PingFlags =
    /// Indicates that this PING frame is a PING response.
    | ACK = 0x01uy

/// Is a mechanism for measuring a minimal round-trip time from the sender, as well as determining whether an idle
// connection is still functional. Can be sent from any endpoint.
[<Struct>]
type PingBody =
    {
        /// 8 octets of opaque data in the frame payload.
        /// A sender can include any value it chooses and use those octets in any fashion.
        OpaqueData: Octets
    }
