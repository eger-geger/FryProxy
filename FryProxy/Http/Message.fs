[<RequireQualifiedAccess>]
module FryProxy.Http.Message

open System.IO
open System.Text
open FryProxy.IO


let httpMetadataWriter dst =
    new StreamWriter(dst, Encoding.ASCII, -1, true, NewLine = "\r\n")


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
let writeHeader (startLine, headers: Field list) (stream: Stream) =
    task {
        use writer = httpMetadataWriter stream

        do! writer.WriteLineAsync(StartLine.encode startLine)

        for h in headers do
            do! writer.WriteLineAsync(Field.encode h)

        do! writer.WriteLineAsync()
    }

/// Matches upfront known content of non-zero size
let (|HasContentLength|_|) headers =
    ContentLength.TryFind headers
    |> Option.map (_.ContentLength)
    |> Option.bind (fun l -> if l > 0UL then Some l else None)

/// Matches content split into variable-sized chunks
let (|HasChunkedEncoding|_|) headers =
    let encoding =
        TransferEncoding.TryFind headers
        |> Option.map (_.TransferEncoding)
        |> Option.bind List.tryLast

    match encoding with
    | Some "chunked" -> Some HasChunkedEncoding
    | _ -> None

/// Determine kind of message body based on headers.
let inferBodyType headers =
    match headers with
    | HasContentLength 0UL -> Empty
    | HasContentLength length -> Content length
    | HasChunkedEncoding -> Chunked
    | _ -> Empty
