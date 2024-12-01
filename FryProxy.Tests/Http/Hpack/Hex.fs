module FryProxy.Tests.Http.Hpack.Hex

open System

let decodeArr (hex: string) =
    let rec loop (s: string) acc =
        if String.IsNullOrEmpty s then
            acc |> List.rev
        else
            Convert.ToByte(s[0..1], 16) :: acc |> loop s[2..]

    loop (hex.Replace(" ", "")) List.empty |> List.toArray