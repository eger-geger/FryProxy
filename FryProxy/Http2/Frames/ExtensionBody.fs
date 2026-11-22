namespace FryProxy.Http2.Frames

open System
open System.Diagnostics
open FryProxy.Http2

/// Body for extension frame types not defined in RFC 7540.
[<Struct; CustomEquality; NoComparison; StructuredFormatDisplay("{DisplayText}"); DebuggerDisplay("{DisplayText,nq}")>]
type ExtensionBody =
    { ExtensionData: Octets }

    member this.DisplayText =
        $"{{ ExtensionData = [{this.ExtensionData.Size}] }}"

    interface IEquatable<ExtensionBody> with
        member this.Equals(other: ExtensionBody) =
            this.ExtensionData.Size = other.ExtensionData.Size

    override this.Equals(obj) =
        match obj with
        | :? ExtensionBody as other -> (this :> IEquatable<ExtensionBody>).Equals(other)
        | _ -> false

    override this.GetHashCode() = HashCode.Combine(this.ExtensionData.Size)

    override this.ToString() = this.DisplayText
