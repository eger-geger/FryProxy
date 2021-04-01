namespace FryProxy.Http

open System
open System.Text

type HttpHeader = { name: string; values: string list }

module Header =

    let create name values = { name = name; values = values }

    let parseValue (value: string) =
        value.Split [| ',' |]
        |> Array.map (fun s -> s.Trim())
        |> Array.filter String.isNotBlank
        |> Array.toList

    let tryParse (line: string) =
        match line.Split([| ':' |], 2) with
        | [| name; value |] when String.isNotBlank value -> Some { name = name; values = parseValue value }
        | _ -> None

    let hasName name (header: HttpHeader) = String.Equals(header.name, name, StringComparison.OrdinalIgnoreCase)
    
    let tryLast (header: HttpHeader) = List.tryLast header.values
    
    let toString (header: HttpHeader) =
        let values = String.concat ", " header.values
        $"{header.name}: {values}"

    module Names =
        let Host = "Host"
        let ContentType = "Content-Type"
        let ContentLength = "Content-Length"

    module ContentLength =

        let create (length: int64) = create Names.ContentLength [ length.ToString() ]

    module ContentType =

        let textPlain (enc: Encoding) = create Names.ContentType [ $"text/plain; encoding={enc.BodyName}" ]
