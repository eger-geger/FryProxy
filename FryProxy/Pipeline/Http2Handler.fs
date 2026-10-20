module FryProxy.Pipeline.Http2Handler

open System.IO.Pipelines
open System.Net.Http
open System.Threading.Channels
open System.Threading.Tasks
open FryProxy.Http
open FryProxy.Http2
open System.Net
open FryProxy.Http.Fields
open FryProxy.Http2.Fields
open FryProxy.Http2.Frames
open FryProxy.Http2.Hpack
open FryProxy.IO
open FryProxy.IO.BufferedParser
open FryProxy.Pipeline.Failures
open FryProxy.Pipeline.RequestHandler


let tryParseConnectionFrame (state: ConnectionState voption) : ConnectionStateTransition voption Parser =
    let cnx = state |> ValueOption.defaultValue ServerConnection.Empty

    Parse.frame
    |> Parser.map (ServerConnection.transition cnx)
    |> Parser.map ValueSome

let tryDecodeRequestLine fields =
    let path, rem = TryPop<Path> fields
    let method, rem = TryPop<Method> rem
    let authority, rem = TryPop<Authority> rem

    match struct (method, authority, path) with
    | Some({ Method = m }, _), Some({ Authority = a }, _), Some({ Path = p }, _) when m <> HttpMethod.Connect ->
        ValueSome({ Method = m; Target = p; Version = Protocol.Http2 }, FieldOf { Host = a } :: rem)
    | Some({ Method = m }, _), Some({ Authority = a }, _), Option.None when m = HttpMethod.Connect ->
        ValueSome({ Method = m; Target = a; Version = Protocol.Http2 }, FieldOf { Host = a } :: rem)
    | Some({ Method = m }, _), Some({ Authority = a }, _), Option.None when m = HttpMethod.Options ->
        ValueSome({ Method = m; Target = "*"; Version = Protocol.Http2 }, FieldOf { Host = a } :: rem)
    | _ -> ValueNone

let createRequestMessage (fields: FieldPack List) chunks : RequestMessage ValueOption =
    let reqFields = fields |> List.map FieldPack.field

    reqFields
    |> tryDecodeRequestLine
    |> ValueOption.map (fun (requestLine, fields) ->
        { Header = { StartLine = requestLine; Fields = fields }
          Body = Chunked(chunks) })

let private responseWriterLoop (messages: ChannelReader<ResponseMessage>) (dst: PipeWriter) =
    task {
        let mutable interrupted = false
        let mutable fieldTable = Table.empty
        
        let msgIt = messages.ReadAllAsync().GetAsyncEnumerator()
        
        let advance () =
            if interrupted then
                ValueTask.FromResult(false)
            else
                msgIt.MoveNextAsync()

        while! advance () do
            let response = msgIt.Current
            let! result = Encode.frame Unchecked.defaultof<Frame> dst

            if result.IsCompleted || result.IsCanceled then
                interrupted <- true

        do! dst.CompleteAsync()
    }

let private reply (pipeline: 'ctx RequestHandler) (req: RequestMessage) (chan: ChannelWriter<Frame>) =
    task {
        let! response, ctx = pipeline.Invoke(req)

        do ()
    }

/// Read a request, run it through a pipeline to produce a response and write it back.
/// Return response context produced by the pipeline as a result.
let executePipelineIO (pipeline: 'ctx RequestHandler) (clientBuffer: ReadBuffer) =
    task {
        let responseWriter = PipeWriter.Create(clientBuffer.Stream)
        let responseFrames = Channel.CreateUnbounded<ResponseMessage>()

        let! frameSeq = Parser.unfold tryParseConnectionFrame |> Parser.run clientBuffer
        let frameEnum = frameSeq.GetAsyncEnumerator()
        let chunksChan = Channel.CreateUnbounded<Chunk>()

        let responseMux = responseWriterLoop responseFrames.Reader responseWriter
        do responseMux.Start()

        while! frameEnum.MoveNextAsync() do
            match frameEnum.Current with
            | None -> do ()
            | ConnectionError code -> failwith "todo"
            | StreamError(streamId, code) -> failwith "todo"
            | PingRequest bytes -> failwith "todo"
            | MessageBody(sid, stream) ->
                let chunk: Chunk =
                    { Header = { Size = stream.Size; Extensions = [] }; Body = Content(stream) }

                do! chunksChan.Writer.WriteAsync(chunk)
            | MessageFields(sid, fields) ->
                match createRequestMessage fields (chunksChan.Reader.ReadAllAsync()) with
                | ValueNone -> failwith "todo"
                | ValueSome message ->
                    let handler =
                        task {
                            let! response = pipeline.Invoke(message)
                            do ()
                        }

                    do handler.Start()
            | StreamReset(sid, code) -> failwith "todo"
            | ConnectionClose code -> failwith "todo"
            | ConnectionWindowUpdate increment -> failwith "todo"
            | StreamWindowUpdate(streamId, increment) -> failwith "todo"
            | SettingsAck -> failwith "todo"
            | ClientSettings settings -> failwith "todo"

        failwith "Not implemented"
    }
