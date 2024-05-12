namespace FryProxy.Http

open System

[<Struct>]
type Field = { Name: string; Values: string list }

[<RequireQualifiedAccess>]
module Field =

    /// Curried factory accepting name and values
    let create name values = { Name = name; Values = values }

    let private isNonEmpty = String.IsNullOrWhiteSpace >> not

    /// Decode comma-separated field values.
    let decodeValues (value: string) =
        value.Split [| ',' |]
        |> Array.map (_.Trim())
        |> Array.filter isNonEmpty
        |> Array.toList

    /// Attempt to decode a line as a folded continuation of a field value.
    let tryFoldedLine (line: string) =
        let isFolded = [ Tokens.WS; Tokens.HTAB ] |> List.exists line.StartsWith
        if isFolded then Some(line.Trim()) else None

    let trySplitNameValue (line: string) =
        match line.Split([| ':' |], 2) with
        | [| name; value |] when name.Trim().Length = name.Length -> Some(name, value)
        | _ -> None

    /// Encode HTTP field for transmission.
    let encode (header: Field) =
        let values = String.concat ", " header.Values
        $"{header.Name}: {values}"

    /// Attempt to find HTTP header by name.
    let tryFind name (headers: Field list) =
        headers |> List.tryFind (fun h -> h.Name = name)

type Field with

    member field.Encode() = Field.encode field
