namespace FryProxy.Http2.Frames

open System
open System.Diagnostics


/// Is a mechanism for measuring a minimal round-trip time from the sender, as well as determining whether an idle
/// connection is still functional. Can be sent from any endpoint.
[<Struct; CustomEquality; NoComparison; StructuredFormatDisplay("{DisplayText}"); DebuggerDisplay("{DisplayText,nq}")>]
type PingBody =
    {
        /// 8 octets of opaque data in the frame payload.
        /// A sender can include any value it chooses and use those octets in any fashion.
        Data: byte ReadOnlyMemory
    }

    member this.DisplayText =
        $"{{ Data = {Convert.ToHexString(this.Data.Span)} }}"

    interface IEquatable<PingBody> with
        member this.Equals(other: PingBody) =
            this.Data.Length = other.Data.Length
            && this.Data.Span.SequenceEqual(other.Data.Span)

    override this.Equals(obj) =
        match obj with
        | :? PingBody as other -> (this :> IEquatable<PingBody>).Equals(other)
        | _ -> false

    override this.GetHashCode() = HashCode.Combine(this.Data.Length)

    override this.ToString() = this.DisplayText
