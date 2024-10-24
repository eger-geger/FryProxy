namespace FryProxy.Http

open System
open System.IO
open FryProxy.IO
open Microsoft.FSharp.Core

[<Struct>]
type ChunkHeader = { Size: uint64; Extensions: string list }

[<Struct>]
type ChunkBody =
    | Content of Bytes: IByteBuffer
    | Trailer of Fields: Field list

[<Struct>]
type Chunk = { Header: ChunkHeader; Body: ChunkBody }

[<RequireQualifiedAccess>]
module ChunkHeader =

    /// Attempt to read chunk size and extensions from string
    let tryDecode (line: string) =
        match line.Trim().Split(';') |> List.ofArray with
        | size :: ext ->
            try
                let size = Convert.ToUInt64(size, 16)
                let ext = ext |> List.map(_.Trim())
                { Size = size; Extensions = ext } |> Some
            with _ ->
                None
        | [] -> None

    /// Convert chunk size and extensions to string
    let encode { Size = size; Extensions = ext } : string = String.Join(';', $"{size:X}" :: ext)

type ChunkHeader with

    member header.Encode() = ChunkHeader.encode header


module Chunk =

    /// Write a chunk to a stream copying its body from the buffer.
    /// Number of copied bytes is determined by the chunk header.
    let write (chunk: Chunk) (wr: StreamWriter) =
        task {
            do! wr.WriteLineAsync(chunk.Header.Encode())

            match chunk.Body with
            | Content bytes ->
                do! wr.FlushAsync()
                do! bytes.WriteAsync(wr.BaseStream)
            | Trailer fields ->
                for field in fields do
                    do! wr.WriteLineAsync(field.Encode())

            do! wr.WriteLineAsync()
        }
