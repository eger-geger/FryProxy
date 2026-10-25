namespace FryProxy.Http2.Frames

open System.Diagnostics
open FryProxy.Http2

/// Deprecated frame type preserved for interoperability.
[<Struct; StructuredFormatDisplay("{DisplayText}"); DebuggerDisplay("{DisplayText,nq}")>]
type PriorityBody =
    { Exclusive: bool; Dependency: StreamId; Weight: uint8 }

    member this.DisplayText =
        $"{{ Dependency = #{this.Dependency}; Weight = {this.Weight}; Exclusive = {this.Exclusive} }}"

    override this.ToString() = this.DisplayText