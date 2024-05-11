module FryProxy.Http.Message

open System
open System.IO
open System.Text
open FryProxy.IO


let httpMetadataWriter dst =
    new StreamWriter(dst, Encoding.ASCII, -1, true, NewLine = "\r\n")


[<Struct>]
type ChunkHeader =
    { Size: uint64
      Extensions: string List }

    /// Convert chunk size and extensions to string
    member this.Encode() : string =
        String.Join(';', $"{this.Size:X}" :: this.Extensions)

    /// Attempt to read chunk size and extensions from string
    static member TryDecode(line: string) =
        match line.Trim().Split(';') |> List.ofArray with
        | size :: ext ->
            try
                let size = Convert.ToUInt64(size, 16)
                let ext = ext |> List.map (_.Trim())
                { Size = size; Extensions = ext } |> Some
            with _ ->
                None
        | [] -> None

/// Write a chunk to a stream copying its body from the buffer.
/// Number of copied bytes is determined by the chunk header.
let copyChunk (dst: Stream) (header: ChunkHeader) (src: ReadBuffer<_>) =
    task {
        use writer = httpMetadataWriter dst

        do! header.Encode() |> writer.WriteLineAsync
        do! writer.FlushAsync()

        do! src.Copy header.Size dst
        do! writer.WriteLineAsync()
    }


/// Asynchronously write message first line and headers to stream.
let writeHeader (startLine, headers: HttpHeader list) (stream: Stream) =
    task {
        use writer = httpMetadataWriter stream

        do! writer.WriteLineAsync(StartLine.toString startLine)

        for h in headers do
            do! writer.WriteLineAsync(HttpHeader.encode h)

        do! writer.WriteLineAsync()
    }

/// Matches upfront known content of non-zero size
let (|FixedContent|_|) headers =
    HttpHeader.tryFindT<ContentLength> headers
    |> Option.map (_.ContentLength)
    |> Option.bind (fun l -> if l > 0UL then Some l else None)

/// Matches content split into variable-sized chunks
let (|ChunkedContent|_|) headers =
    let encoding =
        HttpHeader.tryFindT<TransferEncoding> headers
        |> Option.map (_.TransferEncoding)
        |> Option.bind List.tryLast

    match encoding with
    | Some "chunked" -> Some ChunkedContent
    | _ -> None
