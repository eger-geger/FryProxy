namespace FryProxy.Http

open System.Collections.Generic
open System.IO
open System.Text
open FryProxy.Http.Fields
open FryProxy.IO

[<Struct>]
type 'L MessageHeader when 'L :> StartLine = Header of line: 'L * fields: Field list
type ResponseHeader = StatusLine MessageHeader
type RequestHeader = RequestLine MessageHeader

[<Struct>]
type MessageBody =
    | Empty
    | Sized of Length: uint64 * Content: IReadOnlyBytes
    | Chunked of Chunks: Chunk IAsyncEnumerable

[<Struct>]
type 'L Message when 'L :> StartLine = Message of Header: 'L MessageHeader * Body: MessageBody
type RequestMessage = RequestLine Message
type ResponseMessage = StatusLine Message 


[<RequireQualifiedAccess>]
module Message =
    
    /// Message metadata writer.
    let inline writer stream =
        new StreamWriter(stream, Encoding.ASCII, -1, true, NewLine = "\r\n")
    
    /// Write chunked message content to stream.
    let writeChunks (chunks: Chunk IAsyncEnumerable) wr =
        task {
            let it = chunks.GetAsyncEnumerator()
            
            while! it.MoveNextAsync() do
                do! Chunk.write it.Current wr
                
            do! it.DisposeAsync()
        }
    
    /// Serialize message header to stream.
    let inline writeHeader (Header(line, fields)) (wr: StreamWriter) =
        task {
            do! wr.WriteLineAsync(line.Encode())

            for h in fields do
                do! wr.WriteLineAsync(Field.encode h)

            do! wr.WriteLineAsync()
        }
    
    /// Serialize message to stream.
    let write (Message(header, body)) stream =
        task {
            use wr = writer stream
            
            do! writeHeader header wr
            
            match body with
            | Empty -> ()
            | Sized (n, bytes) ->
                do! wr.FlushAsync()
                do! bytes.CopyAsync(n, stream)
            | Chunked(chunks) -> do! writeChunks chunks wr
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
