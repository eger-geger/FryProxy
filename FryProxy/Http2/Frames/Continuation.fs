namespace FryProxy.Http2.Frames

open System

type ContinuationFlags =
    /// Indicates that this frame ends a field block.
    | END_HEADERS = 0x04uy

/// Used to continue a sequence of field block fragments.
[<Struct; CustomEquality; NoComparison>]
type ContinuationBody =
    { FieldFragment: byte ReadOnlyMemory }

    interface IEquatable<ContinuationBody> with
        member this.Equals(other: ContinuationBody) =
            this.FieldFragment.Length = other.FieldFragment.Length
            && this.FieldFragment.Span.SequenceEqual(other.FieldFragment.Span)

    override this.Equals(obj) =
        match obj with
        | :? ContinuationBody as other -> (this :> IEquatable<ContinuationBody>).Equals(other)
        | _ -> false

    override this.GetHashCode() =
        HashCode.Combine(this.FieldFragment.Length)
