namespace FryProxy.Http

open System.IO
open System.Text
open FryProxy.IO

[<Struct>]
type 'L MessageHeader when 'L :> StartLine = Header of line: 'L * fields: Field List

/// Type of HTTP message body
type MessageBodyType =
    | Empty
    | Content of length: uint64
    | Chunked

[<RequireQualifiedAccess>]
module Message =

    let httpMetadataWriter dst =
        new StreamWriter(dst, Encoding.ASCII, -1, true, NewLine = "\r\n")


    /// Write a chunk to a stream copying its body from the buffer.
    /// Number of copied bytes is determined by the chunk header.
    let copyChunk (dst: Stream) (header: ChunkHeader, trailer: Field list) (src: ReadBuffer<_>) =
        task {
            use writer = httpMetadataWriter dst

            do! header.Encode() |> writer.WriteLineAsync
            do! writer.FlushAsync()

            if header.Size > 0UL then
                do! src.Copy header.Size dst
            else
                for f in trailer do
                    do! writer.WriteLineAsync(f.Encode())

            do! writer.WriteLineAsync()
        }


    /// Asynchronously write message first line and headers to stream.
    let writeHeader (Header(line, fields)) (stream: Stream) =
        task {
            use writer = httpMetadataWriter stream

            do! writer.WriteLineAsync(line.Encode())

            for h in fields do
                do! writer.WriteLineAsync(Field.encode h)

            do! writer.WriteLineAsync()
        }

    /// Matches upfront known content of non-zero size
    let (|HasContentLength|_|) fields =
        ContentLength.TryFind fields
        |> Option.map (_.ContentLength)
        |> Option.bind (fun l -> if l > 0UL then Some l else None)

    /// Matches content split into variable-sized chunks
    let (|HasChunkedEncoding|_|) fields =
        let encoding =
            TransferEncoding.TryFind fields
            |> Option.map (_.TransferEncoding)
            |> Option.bind List.tryLast

        match encoding with
        | Some "chunked" -> Some HasChunkedEncoding
        | _ -> None

    /// Determine kind of message body based on headers.
    let inferBodyType fields =
        match fields with
        | HasContentLength 0UL -> Empty
        | HasContentLength length -> Content length
        | HasChunkedEncoding -> Chunked
        | _ -> Empty
