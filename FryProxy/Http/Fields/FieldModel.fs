namespace FryProxy.Http.Fields

open FryProxy.Http

#nowarn "3535"

type 'F IFieldModel when 'F :> IFieldModel<'F> =

    /// Field name
    static abstract Name: string

    /// Convert model value to field values.
    abstract Encode: unit -> string list

    /// Attempt to decode model value from field values.
    static abstract TryDecode: string list -> 'F option


[<AutoOpen>]
module FieldModel =

    type IFieldModel<'F> when 'F :> IFieldModel<'F> with

        /// Covert the model to field.
        member this.ToField() =
            { Name = 'F.Name; Values = this.Encode() }

        /// Attempt to find current field in a list.
        static member TryFind fields =
            fields
            |> Field.tryFind 'F.Name
            |> Option.map (_.Values)
            |> Option.bind 'F.TryDecode

        static member inline op_Implicit(m: #IFieldModel<_>) : Field = m.ToField()
