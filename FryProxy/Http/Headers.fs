module FryProxy.Http.Headers

let createHttpHeader name values = { name = name; values = values }

let parseHttpHeaderValue (value: string) =
    value.Split [| ',' |]
    |> Array.map (fun s -> s.Trim())
    |> Array.filter String.isNotBlank
    |> Array.toList

let tryParseHttpHeader (line: string) =
    match line.Split([| ':' |], 2) with
    | [| name; value |] when String.isNotBlank value -> Some { name = name; values = parseHttpHeaderValue value }
    | _ -> None