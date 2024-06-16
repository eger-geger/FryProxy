[<RequireQualifiedAccess>]
module FryProxy.Http.Request

open System
open System.Net.Http
open FryProxy.Http.Fields


/// Attempt to split authority into host and port, using the given default port if one is omitted.
/// Returns None for malformed authority.
let trySplitHostPort (authority: string) =
    match authority.Split(':') with
    | [| host |] -> Some(host, ValueNone)
    | [| host; port |] ->
        Int32.TryParse(port)
        |> Option.ofAttempt
        |> Option.map(fun port -> host, ValueSome port)
    | _ -> None

/// Resolve requested resource identifier based on information from first line and headers.
let tryResolveTarget (Header(line, fields)) : Resource option =
    if line.method = HttpMethod.Connect then //TODO: unit test
        trySplitHostPort(line.uri.OriginalString)
        |> Option.map(fun (host, port) -> { Host = host; Port = port })
    elif line.uri.IsAbsoluteUri then
        { Host = line.uri.Host; Port = ValueSome(line.uri.Port) } |> Some
    else
        fields
        |> Host.TryFind
        |> Option.map(_.Host)
        |> Option.bind(trySplitHostPort)
        |> Option.map(fun (host, port) -> { Host = host; Port = port })
