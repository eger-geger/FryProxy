[<RequireQualifiedAccess>]
module FryProxy.Http.Request

open System

type Header = RequestLine MessageHeader

/// Attempt to split authority into host and port, using the given default port if one is omitted.
/// Returns None for malformed authority.
let trySplitHostPort (defaultPort: int) (authority: string) =
    match authority.Split(':') with
    | [| host |] -> Some(host, defaultPort)
    | [| host; port |] -> Int32.TryParse(port) |> Option.ofAttempt |> Option.map (fun port -> host, port)
    | _ -> None

/// Resolve requested resource identifier based on information from first line and headers.
let tryResolveResource (defaultPort: int) (Header(line, fields)) : Resource option =
    if line.uri.IsAbsoluteUri then
        { Host = line.uri.Host
          Port = line.uri.Port
          AbsoluteRef = line.uri.PathAndQuery }
        |> Some
    else
        fields
        |> Host.TryFind
        |> Option.map (_.Host)
        |> Option.bind (trySplitHostPort defaultPort)
        |> Option.map (fun (host, port) -> { Host = host; Port = port; AbsoluteRef = line.uri.OriginalString })
