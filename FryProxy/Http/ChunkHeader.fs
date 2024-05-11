namespace FryProxy.Http

open System
open Microsoft.FSharp.Core

[<Struct>]
type ChunkHeader = { Size: uint64; Extensions: string List }

[<RequireQualifiedAccess>]
module ChunkHeader =

    /// Attempt to read chunk size and extensions from string
    let tryDecode (line: string) =
        match line.Trim().Split(';') |> List.ofArray with
        | size :: ext ->
            try
                let size = Convert.ToUInt64(size, 16)
                let ext = ext |> List.map (_.Trim())
                { Size = size; Extensions = ext } |> Some
            with _ ->
                None
        | [] -> None

    /// Convert chunk size and extensions to string
    let encode header : string =
        String.Join(';', $"{header.Size:X}" :: header.Extensions)

type ChunkHeader with

    member header.Encode() = ChunkHeader.encode header
