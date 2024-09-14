namespace FryProxy.Http.Fields

open FryProxy.Http

#nowarn "3535" "3536"

type 'F IFieldModel when 'F :> IFieldModel<'F> =

    /// Field name
    static abstract Name: string

    /// Convert model value to field values.
    abstract Encode: unit -> string list

    /// Attempt to decode model value from field values.
    static abstract TryDecode: string list -> 'F option


[<AutoOpen>]
module FieldModel =

    /// Resolve the name of a filed model type.
    let inline NameOf<'F when 'F :> 'F IFieldModel> = 'F.Name

    /// Encodes a field model to a field.
    let inline FieldOf (model: 'F IFieldModel) =
        { Name = 'F.Name; Values = model.Encode() }

    type IFieldModel<'F> when 'F :> IFieldModel<'F> with
    
        static member FromField fld : 'F option =
            if fld.Name = 'F.Name then
                'F.TryDecode fld.Values
            else
                None

        /// Attempt to find current field in a list.
        static member TryFind fields =
            fields
            |> Field.tryFind 'F.Name
            |> Option.map(_.Values)
            |> Option.bind 'F.TryDecode

        /// Attempt to remove a field from the list returning both the field and updated list.
        static member TryPop fields : 'F option * Field list =
            match fields |> List.tryFindIndex(fun f -> f.Name = 'F.Name) with
            | Some i ->
                let front, back = List.splitAt i fields
                'F.FromField(List.head back), front @ List.tail back
            | None -> None, fields
