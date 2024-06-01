namespace FryProxy.Http

open System
open System.IO
open FryProxy.IO
open Microsoft.FSharp.Core

[<Struct>]
type ChunkHeader = ChunkHeader of Size: uint64 * Extensions: string list

[<Struct>]
type ChunkBody =
    | Content of Bytes: IReadOnlyBytes
    | Trailer of Fields: Field list

[<Struct>]
type Chunk = Chunk of Header: ChunkHeader * Body: ChunkBody

[<RequireQualifiedAccess>]
module ChunkHeader =

    /// Attempt to read chunk size and extensions from string
    let tryDecode (line: string) =
        match line.Trim().Split(';') |> List.ofArray with
        | size :: ext ->
            try
                let size = Convert.ToUInt64(size, 16)
                let ext = ext |> List.map (_.Trim())
                ChunkHeader(size, ext) |> Some
            with _ ->
                None
        | [] -> None

    /// Convert chunk size and extensions to string
    let encode (ChunkHeader(size, ext)) : string = String.Join(';', $"{size:X}" :: ext)

type ChunkHeader with

    member header.Encode() = ChunkHeader.encode header


module Chunk =

    /// Write a chunk to a stream copying its body from the buffer.
    /// Number of copied bytes is determined by the chunk header.
    let write (Chunk(ChunkHeader(size, _) as header, body)) (wr: StreamWriter) =
        task {
            do! wr.WriteLineAsync(header.Encode())

            match body with
            | Content bytes ->
                do! wr.FlushAsync()
                do! bytes.CopyAsync(size, wr.BaseStream)
            | Trailer fields ->
                for field in fields do
                    do! wr.WriteLineAsync(field.Encode())

            do! wr.WriteLineAsync()
        }
