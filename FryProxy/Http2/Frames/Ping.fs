namespace FryProxy.Http2.Frames

open System

[<Flags>]
type PingFlags =
    /// Indicates that this PING frame is a PING response.
    | ACK = 0x01uy

/// Is a mechanism for measuring a minimal round-trip time from the sender, as well as determining whether an idle
// connection is still functional. Can be sent from any endpoint.
[<Struct; CustomEquality; NoComparison>]
type PingBody =
    {
        /// 8 octets of opaque data in the frame payload.
        /// A sender can include any value it chooses and use those octets in any fashion.
        Data: byte ReadOnlyMemory
    }

    interface IEquatable<PingBody> with
        member this.Equals(other: PingBody) =
            this.Data.Length = other.Data.Length
            && this.Data.Span.SequenceEqual(other.Data.Span)

    override this.Equals(obj) =
        match obj with
        | :? PingBody as other -> (this :> IEquatable<PingBody>).Equals(other)
        | _ -> false

    override this.GetHashCode() = HashCode.Combine(this.Data.Length)
