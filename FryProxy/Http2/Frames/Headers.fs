namespace FryProxy.Http2.Frames

open System
open System.Diagnostics
open FryProxy.Http2


///  Is used to open a stream, and additionally carries a field block fragment.
/// Despite the name, a HEADERS frame can carry a header section or a trailer section.
[<Struct; CustomEquality; NoComparison; StructuredFormatDisplay("{DisplayText}"); DebuggerDisplay("{DisplayText,nq}")>]
type HeadersBody =
    {
        ///  Length of the frame padding in units of octets. This field is only present if the PADDED flag is set.
        PadLength: uint8
        /// This field is only present if the PRIORITY flag is set. Priority signals in HEADERS frames are deprecated.
        Exclusive: bool
        /// A 31-bit stream identifier. This field is only present if the PRIORITY flag is set.
        Dependency: StreamId
        /// This field is only present if the PRIORITY flag is set.
        Weight: uint8
        /// Field block.
        FieldFragment: byte ReadOnlyMemory
    }

    member this.DisplayText =
        let padStr = if this.PadLength = 0uy then "" else $"PadLength = {this.PadLength}; "
        let priorityStr =
            if this.Dependency = 0u && this.Weight = 0uy && not this.Exclusive then ""
            else $"Dependency = #{this.Dependency}; Weight = {this.Weight}; Exclusive = {this.Exclusive}; "
        $"{{ {padStr}{priorityStr}FieldFragment = [{this.FieldFragment.Length}] }}"

    interface IEquatable<HeadersBody> with
        member this.Equals(other: HeadersBody) =
            this.PadLength = other.PadLength
            && this.Exclusive = other.Exclusive
            && this.Dependency = other.Dependency
            && this.Weight = other.Weight
            && this.FieldFragment.Length = other.FieldFragment.Length
            && this.FieldFragment.Span.SequenceEqual(other.FieldFragment.Span)

    override this.Equals(obj) =
        match obj with
        | :? HeadersBody as other -> (this :> IEquatable<HeadersBody>).Equals(other)
        | _ -> false

    override this.GetHashCode() =
        HashCode.Combine(this.PadLength, this.Exclusive, this.Dependency, this.Weight, this.FieldFragment.Length)

    override this.ToString() = this.DisplayText
