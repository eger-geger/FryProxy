namespace FryProxy.Http

open System


type HttpHeader = { Name: string; Values: string list }

module HttpHeader =

    /// Curried factory accepting name and values
    let create name values = { Name = name; Values = values }

    /// Parse header values from string.
    let parseValues (value: string) =
        value.Split [| ',' |]
        |> Array.map (_.Trim())
        |> Array.filter (String.IsNullOrWhiteSpace >> not)
        |> Array.toList

    /// Return HTTP header parsed from string or None.
    let tryDecode (line: string) =
        match line.Split([| ':' |], 2) with
        | [| name; value |] when String.IsNullOrWhiteSpace value |> not ->
            Some { Name = name; Values = parseValues value }
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

    /// Attempt to find an HTTP header in a list and convert it to a model.
    let inline tryFindT<'a
        when 'a: (static member Name: string) and 'a: (static member TryDecode: string list -> 'a option)>
        headers
        =
        headers |> tryFind 'a.Name |> Option.map (_.Values) |> Option.bind 'a.TryDecode

    /// Convert HTTP header model to generic variant.
    let inline fromT<'a when 'a: (static member Name: string) and 'a: (member Encode: unit -> string list)> (a: 'a) =
        { Name = 'a.Name; Values = a.Encode() }
