namespace FryProxy.Http2.Frames

open System
open System.Diagnostics
open FryProxy.Http2


/// Notify the peer endpoint in advance of streams the sender intends to initiate.
[<Struct; CustomEquality; NoComparison; StructuredFormatDisplay("{DisplayText}"); DebuggerDisplay("{DisplayText,nq}")>]
type PushPromiseBody =
    {
        /// A field containing the length of the frame padding in units of octets. Only present if the PADDED flag is set.
        PadLength: uint8

        /// Identifies the stream that is reserved by the PUSH_PROMISE.
        /// The promised stream identifier MUST be a valid choice for the next stream sent by the sender.
        Promised: StreamId

        /// A field block fragment containing the request control data and a header section.
        FieldBlock: byte ReadOnlyMemory
    }

    member this.DisplayText =
        let padStr = if this.PadLength = 0uy then "" else $"PadLength = {this.PadLength}; "
        $"{{ {padStr}Promised = #{this.Promised}; FieldBlock = [{this.FieldBlock.Length}] }}"

    interface IEquatable<PushPromiseBody> with
        member this.Equals(other: PushPromiseBody) =
            this.PadLength = other.PadLength
            && this.Promised = other.Promised
            && this.FieldBlock.Length = other.FieldBlock.Length
            && this.FieldBlock.Span.SequenceEqual(other.FieldBlock.Span)

    override this.Equals(obj) =
        match obj with
        | :? PushPromiseBody as other -> (this :> IEquatable<PushPromiseBody>).Equals(other)
        | _ -> false

    override this.GetHashCode() =
        HashCode.Combine(this.PadLength, this.Promised, this.FieldBlock.Length)

    override this.ToString() = this.DisplayText
