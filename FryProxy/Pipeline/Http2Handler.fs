module FryProxy.Pipeline.Http2Handler

open System
open System.IO.Pipelines
open System.Net.Http
open System.Threading
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

open System.Diagnostics

let private trace (msg: string) = Trace.TraceInformation(msg)

let tryParseConnectionFrame (state: ConnectionState voption) : ConnectionStateTransition voption Parser =
    let cnx = state |> ValueOption.defaultValue ServerConnection.Empty

    Parse.frame
    |> Parser.map (ServerConnection.transition cnx)
    |> Parser.map ValueSome

let tryDecodeRequestLine fields =
    let path, rem = TryPop<Path> fields
    let method, rem = TryPop<Method> rem
    let authority, rem = TryPop<Authority> rem
    let _, rem = TryPop<Scheme> rem

    match struct (method, authority, path) with
    | Some({ Method = m }, _), Some({ Authority = a }, _), Some({ Path = p }, _) when m <> HttpMethod.Connect ->
        ValueSome({ Method = m; Target = p; Version = Protocol.Http2 }, FieldOf { Host = a } :: rem)
    | Some({ Method = m }, _), Some({ Authority = a }, _), Option.None when m = HttpMethod.Connect ->
        ValueSome({ Method = m; Target = a; Version = Protocol.Http2 }, FieldOf { Host = a } :: rem)
    | Some({ Method = m }, _), Some({ Authority = a }, _), Option.None when m = HttpMethod.Options ->
        ValueSome({ Method = m; Target = "*"; Version = Protocol.Http2 }, FieldOf { Host = a } :: rem)
    | _ -> ValueNone

let createRequestMessage (fields: FieldPack list) (chunkReader: ChannelReader<Chunk>) : RequestMessage ValueOption =
    let rawFields = fields |> List.map FieldPack.field

    let body =
        if chunkReader.Completion.IsCompleted then
            Empty
        else
            Chunked(chunkReader.ReadAllAsync())

    let rawFields =
        if body.IsChunked then
            let rawFields = rawFields |> List.filter (fun f -> f.Name <> "content-length")
            FieldOf { TransferEncoding = [ "chunked" ] } :: rawFields
        else
            rawFields

    rawFields
    |> tryDecodeRequestLine
    |> ValueOption.map (fun (rl, fields) ->
        { Header = { StartLine = { rl with Version = Protocol.Http11 }; Fields = fields }
          Body = body })

let private reply
    (pipeline: 'ctx RequestHandler)
    (chan: ChannelWriter<OutboundMessage>)
    (streamId: StreamId)
    (req: RequestMessage)
    =
    task {
        let! response, _ = pipeline.Invoke(req)

        let status = FieldOf { Status = response.Header.StartLine.Code }
        do! chan.WriteAsync(OutboundMessage.Headers(streamId, status :: response.Header.Fields))

        let! body = MessageBody.read response.Body
        let data = Frame.binaryData streamId body |> Frame.withFlags DataFlags.END_STREAM
        do! chan.WriteAsync(OutboundMessage.Frame data)
    }

/// Read a request, run it through a pipeline to produce a response and write it back.
/// Return response context produced by the pipeline as a result.
let executePipelineIO (pipeline: 'ctx RequestHandler) (clientBuffer: ReadBuffer) =
    task {
        let responseWriter = PipeWriter.Create(clientBuffer.Stream)
        let responseChannel = Channel.CreateUnbounded<OutboundMessage>()
        let _ = Http2Writer.run responseChannel.Reader responseWriter
        let replyTo = reply pipeline responseChannel.Writer

        let mutable streamCtx = Map.empty<StreamId, Channel<Chunk>>
        let mutable replies: Task list = []

        let! frameSeq = Parser.unfold tryParseConnectionFrame |> Parser.run clientBuffer
        let frameEnum = frameSeq.GetAsyncEnumerator()

        // Send initial server SETTINGS frame per RFC 9113
        let initialSettings = Frame.settings []
        do! responseChannel.Writer.WriteAsync(OutboundMessage.Frame initialSettings)

        try
            while! frameEnum.MoveNextAsync() do
                replies <- List.filter (fun t -> not t.IsCompleted) replies
                let struct (conn, transition) = frameEnum.Current
                trace $"[DEMUX] {transition}"

                match transition with
                | None -> do ()
                | ConnectionError code ->
                    let lastSid = conn.LastStreamId |> ValueOption.defaultValue 0u
                    let goAway = Frame.goAway lastSid code
                    do! responseChannel.Writer.WriteAsync(OutboundMessage.Frame goAway)
                    responseChannel.Writer.Complete()
                | StreamError(streamId, code) ->
                    let rst = Frame.reset streamId code
                    do! responseChannel.Writer.WriteAsync(OutboundMessage.Frame rst)
                | PingRequest bytes ->
                    let pong = Frame.ping bytes |> Frame.withFlags PingFlags.ACK
                    do! responseChannel.Writer.WriteAsync(OutboundMessage.Frame pong)
                | MessageBody(sid, content, eos) ->
                    let chunkChan = Map.find sid streamCtx
                    do! chunkChan.Writer.WriteAsync(Chunk.ofBuffer content)

                    match content with
                    | :? IConsumable as streaming ->
                        trace "[DEMUX] waiting for streaming body to be consumed before advancing"
                        do! streaming.Consumed
                    | _ -> ()

                    if eos then
                        do! chunkChan.Writer.WriteAsync(Chunk.empty)
                        do chunkChan.Writer.Complete()
                        streamCtx <- Map.remove sid streamCtx
                | MessageFields(sid, fields, eos) ->
                    let chunkChan = Channel.CreateUnbounded<Chunk>()

                    if eos then
                        do chunkChan.Writer.Complete()
                    else
                        streamCtx <- Map.add sid chunkChan streamCtx

                    match createRequestMessage fields chunkChan.Reader with
                    | ValueNone -> trace "[DEMUX] malformed request"
                    | ValueSome message ->
                        trace $"[DEMUX] starting reply for stream={sid}"
                        replies <- (replyTo sid message) :: replies
                | StreamReset(sid, code) -> trace $"[DEMUX] StreamReset: stream={sid} code={code}"
                | ConnectionClose code -> responseChannel.Writer.Complete()
                | ConnectionWindowUpdate increment -> ()
                | StreamWindowUpdate(streamId, increment) -> ()
                | SettingsAck -> ()
                | ClientSettings settings ->
                    let ackFrame = Frame.settings [] |> Frame.withFlags SettingsFlags.ACK
                    do! responseChannel.Writer.WriteAsync(OutboundMessage.Frame ackFrame)

                ()
        finally
            trace "[DEMUX] === WHILE LOOP EXITED ==="

            try
                trace $"[Cleanup] streamCtx count = {Map.count streamCtx}, inFlight count = {List.length replies}"

                // Complete all pending body channels so in-flight replies can finish
                let clientGone = TaskCanceledException "Client gone"
                do streamCtx |> Map.values |> Seq.iter _.Writer.Complete(clientGone)
                trace "[Cleanup] all pending body channels completed"

                // Wait for all in-flight replies to complete before returning
                let! _ = Task.WhenAll(replies)
                trace "[Cleanup] all in-flight replies completed"
            with ex ->
                trace $"[Finally] exception: {ex.Message}"
    }
