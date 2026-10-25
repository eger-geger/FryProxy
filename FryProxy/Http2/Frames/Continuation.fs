namespace FryProxy.Http2.Frames

open System
open System.Diagnostics


/// Used to continue a sequence of field block fragments.
[<Struct; CustomEquality; NoComparison; StructuredFormatDisplay("{DisplayText}"); DebuggerDisplay("{DisplayText,nq}")>]
type ContinuationBody =
    { FieldFragment: byte ReadOnlyMemory }

    member this.DisplayText =
        $"{{ FieldFragment = [{this.FieldFragment.Length}] }}"

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

    override this.ToString() = this.DisplayText
