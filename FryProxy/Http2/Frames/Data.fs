namespace FryProxy.Http2.Frames

open System

[<Flags>]
type DataFlags =
    /// Indicates that the Pad Length field and any padding that it describes are present.
    | PADDED = 0x08uy
    ///  Indicates that this frame is the last that the endpoint will send for the identified stream.
    /// Setting this flag causes the stream to enter one of the "half-closed" states or the "closed" state.
    | END_STREAM = 0x01uy


/// Convey arbitrary, variable-length sequences of octets associated with a stream.
/// One or more DATA frames are used, for instance, to carry HTTP request or response message contents.
[<Struct; CustomEquality; NoComparison>]
type DataBody =
    {
        /// The length of the frame padding in units of octets.
        /// This field is conditional and is only present if the PADDED flag is set.
        PadLength: uint8
        /// Application data.
        /// The amount of data is the remainder of the frame payload after subtracting the length of the other fields that are present.
        Data: byte ReadOnlyMemory
    }

    interface IEquatable<DataBody> with
        member this.Equals(other: DataBody) =
            this.PadLength = other.PadLength
            && this.Data.Length = other.Data.Length
            && this.Data.Span.SequenceEqual(other.Data.Span)

    override this.Equals(obj) =
        match obj with
        | :? DataBody as other -> (this :> IEquatable<DataBody>).Equals(other)
        | _ -> false

    override this.GetHashCode() =
        HashCode.Combine(this.PadLength, this.Data.Length)
