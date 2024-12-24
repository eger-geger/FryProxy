namespace FryProxy.Http.Fields

open FryProxy.Http

#nowarn "3535" "3536"

type 'F IFieldModel when 'F :> IFieldModel<'F> =

    /// Field name
    static abstract Name: string

    /// Convert model value to string filed value.
    abstract Encode: unit -> string

    /// Attempt to decode model value from field values.
    static abstract TryDecode: string -> 'F option


[<AutoOpen>]
module FieldModel =

    /// Resolve the name of a filed model type.
    let inline NameOf<'F when 'F :> 'F IFieldModel> = 'F.Name

    /// Encodes a field model to a field.
    let inline FieldOf (model: 'F IFieldModel) =
        { Name = 'F.Name; Value = model.Encode() }

    let TryFind<'F when 'F :> 'F IFieldModel> fields =
        fields
        |> Field.tryFind 'F.Name
        |> Option.map(_.Value)
        |> Option.bind 'F.TryDecode

    /// Attempt to extract and decode a field from the list.
    let TryPop<'F when 'F :> 'F IFieldModel> fields =
        match fields |> List.tryFindIndex(fun f -> f.Name = 'F.Name) with
        | Some i ->
            let front, back = List.splitAt i fields

            let model =
                back |> List.head |> _.Value |> 'F.TryDecode |> Option.map(fun f -> (f, i))

            model, front @ List.tail back
        | None -> None, fields

    type IFieldModel<'F> when 'F :> IFieldModel<'F> with

        static member FromField fld : 'F option =
            if fld.Name = 'F.Name then
                'F.TryDecode fld.Value
            else
                None

        /// Attempt to find current field in a list.
        static member TryFind fields =
            fields
            |> Field.tryFind 'F.Name
            |> Option.map(_.Value)
            |> Option.bind 'F.TryDecode
