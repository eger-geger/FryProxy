namespace FryProxy.Http

open System
open System.Text

type HttpHeader = { Name: string; Values: string list }

module HttpHeader =

    /// Curried factory accepting name and values
    let create name values = { Name = name; Values = values }

    /// Parse header values from string.
    let parseValues (value: string) =
        value.Split [| ',' |]
        |> Array.map (fun s -> s.Trim())
        |> Array.filter String.isNotBlank
        |> Array.toList

    /// Return HTTP header parsed from string or None.
    let tryDecode (line: string) =
        match line.Split([| ':' |], 2) with
        | [| name; value |] when String.isNotBlank value -> Some { Name = name; Values = parseValues value }
        | _ -> None
    
    /// Return single header value or None if there are multiple.
    let trySingleValue (h: HttpHeader) = List.tryExactlyOne h.Values

    /// Encode HTTP header to string.
    let encode (header: HttpHeader) =
        let values = String.concat ", " header.Values
        $"{header.Name}: {values}"

    /// Attempt to find HTTP header by name.
    let tryFind name (headers: HttpHeader list) =
        headers |> List.tryFind (fun h -> h.Name = name)

module Headers =
    let Host = "Host"
    let ContentType = "Content-Type"
    let ContentLength = "Content-Length"

module ContentLength =

    let create (length: int64) =
        { Name = Headers.ContentLength; Values = [ length.ToString() ] }

module ContentType =

    let textPlain (enc: Encoding) =
        { Name = Headers.ContentType; Values = [ $"text/plain; encoding={enc.BodyName}" ] }
