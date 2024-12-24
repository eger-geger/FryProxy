namespace FryProxy.Http

open System
open Microsoft.FSharp.Core

[<Struct>]
type Field = { Name: string; Value: string }

[<RequireQualifiedAccess>]
module Field =

    /// Curried factory accepting name and values
    let inline create name value = { Name = name; Value = value }

    /// Split comma-separated field values.
    let splitValues (value: string) =
        value.Split [| ',' |]
        |> Array.map(_.Trim())
        |> Array.filter(String.IsNullOrWhiteSpace >> not)
        |> Array.toList

    /// Join multiple field values via comma.
    let inline joinValues (values: string list) = String.Join(", ", values)

    /// Attempt to decode a line as a folded continuation of a field value.
    let tryFoldedLine (line: string) =
        let isFolded = [ Tokens.WS; Tokens.HTAB ] |> List.exists line.StartsWith
        if isFolded then Some(line.Trim()) else None

    let trySplitNameValue (line: string) =
        match line.Split([| ':' |], 2) with
        | [| name; value |] when (String.IsNullOrWhiteSpace >> not) name -> Some(name.Trim(), value.Trim())
        | _ -> None

    /// Encode HTTP field for transmission.
    let inline encode { Name = name; Value = value } = $"{name}: {value}"

    /// Attempt to find HTTP header by name.
    let tryFind name (headers: Field list) =
        headers |> List.tryFind(fun h -> h.Name = name)

    /// Add or update a field in a list.
    let upsert (fn: Field Option -> Field Option) fields =
        let tryUpdate (i, f) =
            Some(f) |> fn |> Option.map(fun f' -> (i, f'))

        match fields |> List.indexed |> List.tryPick tryUpdate with
        | Some(i, fld) -> List.updateAt i fld fields
        | None -> fn None |> Option.map(fun f -> f :: fields) |> Option.defaultValue fields


type Field with

    member field.Encode() = Field.encode field
