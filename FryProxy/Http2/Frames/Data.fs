namespace FryProxy.Http2.Frames

open System.Diagnostics
open FryProxy.Http2



/// Convey arbitrary, variable-length sequences of octets associated with a stream.
/// One or more DATA frames are used, for instance, to carry HTTP request or response message contents.
[<Struct; StructuredFormatDisplay("{DisplayText}"); DebuggerDisplay("{DisplayText,nq}")>]
type DataBody =
    {
        /// The length of the frame padding in units of octets.
        /// This field is conditional and is only present if the PADDED flag is set.
        PadLength: uint8
        /// Application data.
        /// The amount of data is the remainder of the frame payload after subtracting the length of the other fields that are present.
        Data: Octets
    }

    member this.DisplayText =
        let padStr = if this.PadLength = 0uy then "" else $"PadLength = {this.PadLength}; "
        $"{{ {padStr}Data = [{this.Data.Size}] }}"

    override this.ToString() = this.DisplayText
