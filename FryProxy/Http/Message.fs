namespace FryProxy.Http

open System.Collections.Generic
open System.IO
open System.Text
open System.Threading
open System.Threading.Tasks
open FryProxy.Http.Fields
open FryProxy.IO

[<Struct>]
type 'L MessageHeader when 'L :> StartLine = { StartLine: 'L; Fields: Field list }

type ResponseHeader = StatusLine MessageHeader
type RequestHeader = RequestLine MessageHeader

[<Struct>]
type MessageBody =
    | Empty
    | Sized of Content: IByteBuffer
    | Chunked of Chunks: Chunk IAsyncEnumerable

[<Struct>]
type 'L Message when 'L :> StartLine = { Header: 'L MessageHeader; Body: MessageBody }

type RequestMessage = RequestLine Message
type ResponseMessage = StatusLine Message

[<RequireQualifiedAccess>]
module ChunkedBody =

    /// Construct chunked sequence body from synchronous sequence of chunks.
    let fromSeq (chunks: Chunk seq) : MessageBody =
        let asyncEnumerator (ct: CancellationToken) (itr: _ IEnumerator) =
            { new IAsyncEnumerator<_> with
                member _.Current = itr.Current

                member _.MoveNextAsync() =
                    if ct.IsCancellationRequested then
                        ValueTask.FromCanceled<_>(ct)
                    else
                        itr.MoveNext() |> ValueTask.FromResult

                member _.DisposeAsync() =
                    try
                        itr.Dispose()
                        ValueTask.CompletedTask
                    with err ->
                        ValueTask.FromException(err) }

        let asyncEnumerable =
            { new IAsyncEnumerable<Chunk> with
                member _.GetAsyncEnumerator(cancellationToken) =
                    chunks.GetEnumerator() |> asyncEnumerator cancellationToken }

        Chunked asyncEnumerable

[<RequireQualifiedAccess>]
module Message =

    /// Message metadata writer.
    let inline writer stream =
        new StreamWriter(stream, Encoding.ASCII, -1, true, NewLine = "\r\n")

    /// Replace or add a header field in/to a message.
    let withField (fld: Field) (msg: _ Message) =
        let fields = msg.Header.Fields

        let fields' =
            match fields |> List.tryFindIndex(fun f -> f.Name = f.Name) with
            | Some i -> List.updateAt i fld fields
            | None -> fields @ [ fld ]

        { msg with Header.Fields = fields' }

    /// Replace or add a model-defined field in/to a message.
    let withFieldOf (fld: #IFieldModel<_>) = withField(FieldOf fld)

    /// Removes all header fields with a name from the message.
    let withoutField name (msg: _ Message) =
        { msg with
            Header.Fields = msg.Header.Fields |> List.filter(fun f -> f.Name <> name) }

    /// Write chunked message content to stream.
    let writeChunks (chunks: Chunk IAsyncEnumerable) wr =
        task {
            let it = chunks.GetAsyncEnumerator()

            while! it.MoveNextAsync() do
                do! Chunk.write it.Current wr

            do! it.DisposeAsync()
        }

    /// Serialize header to memory.
    let serializeHeader { StartLine = line; Fields = fields } =
        use buffer = new MemoryStream()
        use writer = writer buffer

        do writer.WriteLine(line.Encode())

        for h in fields do
            writer.WriteLine(h.Encode())

        do writer.WriteLine()
        do writer.Flush()
        buffer.ToArray()

    /// Serialize message header to stream.
    let inline writeHeader { StartLine = line; Fields = fields } (wr: StreamWriter) =
        task {
            do! wr.WriteLineAsync(line.Encode())

            for h in fields do
                do! wr.WriteLineAsync(Field.encode h)

            do! wr.WriteLineAsync()
        }

    /// Write message body to a stream (writer).
    let inline writeBody body (wr: StreamWriter) =
        match body with
        | Empty -> ValueTask.CompletedTask
        | Sized content ->
            ValueTask
            <| task {
                do! wr.FlushAsync()
                do! content.WriteAsync wr.BaseStream
            }
        | Chunked(chunks) -> writeChunks chunks wr |> ValueTask

    /// Serialize complete message to stream.
    let write { Header = header; Body = body } stream =
        task {
            use wr = writer stream
            do! writeHeader header wr
            do! writeBody body wr
        }

    /// Matches upfront known content of non-zero size
    let (|HasContentLength|_|) fields =
        ContentLength.TryFind fields
        |> Option.map(_.ContentLength)
        |> Option.bind(fun l -> if l > 0UL then Some l else None)

    /// Matches content split into variable-sized chunks
    let (|HasChunkedEncoding|_|) fields =
        let encoding =
            TransferEncoding.TryFind fields
            |> Option.map(_.TransferEncoding)
            |> Option.bind List.tryLast

        match encoding with
        | Some "chunked" -> Some HasChunkedEncoding
        | _ -> None
