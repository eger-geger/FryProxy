namespace FryProxy.Http

open System

[<Struct>]
type Field = { Name: string; Values: string list }

[<RequireQualifiedAccess>]
module Field =

    /// Curried factory accepting name and values
    let create name values = { Name = name; Values = values }

    /// Decode comma-separated field values.
    let decodeValues (value: string) =
        value.Split [| ',' |]
        |> Array.map (_.Trim())
        |> Array.filter (String.IsNullOrWhiteSpace >> not)
        |> Array.toList

    /// Return HTTP header parsed from string or None.
    let tryDecode (line: string) =
        let nonEmpty = String.IsNullOrWhiteSpace >> not

        match line.Split([| ':' |], 2) with
        | [| name; value |] when nonEmpty value -> Some { Name = name; Values = decodeValues (value.Trim()) }
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
