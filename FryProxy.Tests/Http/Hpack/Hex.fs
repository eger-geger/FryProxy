module FryProxy.Tests.Http.Hpack.Hex

open System

type hex = string

let decodeArr (hex: hex) =
    let rec loop (s: string) acc =
        if String.IsNullOrEmpty s then
            acc |> List.rev
        else
            Convert.ToByte(s[0..1], 16) :: acc |> loop s[2..]

    loop (hex.Replace(" ", "")) [] |> List.toArray

let decodeSpan hex = ReadOnlySpan(decodeArr hex)

/// Convert a byte sequence into 'readable' space-separated hex string.
let encodeSeq (bytes: byte seq) : hex =
    bytes
    |> Seq.map (sprintf "%x")
    |> Seq.chunkBySize 2
    |> Seq.map(String.Concat)
    |> Seq.reduce(sprintf "%s %s")