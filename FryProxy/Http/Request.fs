[<RequireQualifiedAccess>]
module FryProxy.Http.Request

open System
open System.Net.Http
open FryProxy.Extension
open FryProxy.Http.Fields

/// Attempt to split authority into host and port, using the given default port if one is omitted.
/// Returns None for malformed authority.
let trySplitAuthority (authority: string) =
    match authority.Split(':') with
    | [| host |] -> ValueSome(host, ValueNone)
    | [| host; port |] ->
        match Int32.TryParse(port) with
        | true, p -> ValueSome(host, ValueSome(p))
        | _ -> ValueNone
    | _ -> ValueNone

/// Resolve requested resource identifier based on information from first line and headers.
let tryResolveTarget (Header(line, fields)) : Target voption =
    let hostField =
        lazy
            fields
            |> Host.TryFind
            |> Option.map(_.Host)
            |> Option.toValue
            |> ValueOption.bind(trySplitAuthority)
            |> ValueOption.map(fun (host, port) -> { Host = host; Port = port })

    if line.Method = HttpMethod.Connect then
        trySplitAuthority(line.Target)
        |> ValueOption.map(fun (host, port) -> { Host = host; Port = port })
    elif line.Method = HttpMethod.Options && line.Target = "*" then
        hostField.Value
    else
        match Uri.tryParse line.Target with
        | ValueSome url when url.IsAbsoluteUri ->
            { Host = url.Host
              Port =
                if url.IsDefaultPort then
                    ValueNone
                else
                    ValueSome(url.Port) }
            |> ValueSome
        | ValueSome _ -> hostField.Value
        | ValueNone -> ValueNone
