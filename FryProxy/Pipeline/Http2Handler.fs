module FryProxy.Pipeline.Http2Handler

open System
open System.IO.Pipelines
open System.Net.Http
open System.Threading
open System.Threading.Channels
open System.Threading.Tasks
open FryProxy.Extension
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
    (cancel: CancellationToken)
    (h2s: H2Stream)
    (req: RequestMessage)
    =
    task {
        trace $"[REPLY] sending #{h2s.Id} request to upstream"
        let! response, _ = pipeline.Invoke(req)

        let status = FieldOf { Status = response.Header.StartLine.Code }

        if not cancel.IsCancellationRequested then
            trace $"[REPLY] sending status={status} to #{h2s.Id} flow control"
            do! chan.WriteAsync(OutboundMessage.Headers(h2s.Id, status :: response.Header.Fields))

        let! body = MessageBody.read response.Body

        trace $"[REPLY] consuming [{body.Length}] from #{h2s.Id} flow control"
        do! h2s.Window.ConsumeAsync(uint32 body.Length, cancel)
        trace $"[REPLY] consumed [{body.Length}] from #{h2s.Id} flow control"

        if not cancel.IsCancellationRequested then
            let data = Frame.binaryData h2s.Id body |> Frame.withFlags DataFlags.END_STREAM
            trace $"[REPLY] sending data={data} to #{h2s.Id}"
            do! chan.WriteAsync(OutboundMessage.Frame data)
    }

/// Read a request, run it through a pipeline to produce a response and write it back.
/// Return response context produced by the pipeline as a result.
let executePipelineIO (pipeline: 'ctx RequestHandler) (clientBuffer: ReadBuffer) =
    task {
        use connectionCts = new CancellationTokenSource()
        let mutable requestChunks = Map.empty<StreamId, Channel<Chunk>>

        let mutable responders =
            Map.empty<StreamId, struct (Task * CancellationTokenSource)>

        let responseWriter = PipeWriter.Create(clientBuffer.Stream)
        let responseChannel = Channel.CreateUnbounded<OutboundMessage>()
        let _ = Http2Writer.run responseChannel.Reader responseWriter
        let replyTo = reply pipeline responseChannel.Writer

        let! frameSeq = Parser.unfold tryParseConnectionFrame |> Parser.run clientBuffer
        let frameEnum = frameSeq.GetAsyncEnumerator()

        // Send initial server SETTINGS frame per RFC 9113
        let initialSettings =
            Frame.settings [ { Type = SettingType.HEADER_TABLE_SIZE; Value = Table.InitialSize } ]

        do! responseChannel.Writer.WriteAsync(OutboundMessage.Frame initialSettings)

        try
            while! frameEnum.MoveNextAsync() do
                responders <- Map.filter (fun _ struct (task, _) -> not task.IsCompleted) responders
                let struct (conn, transition) = frameEnum.Current
                trace $"[DEMUX] {transition}"

                match transition with
                | None -> do ()
                | ConnectionError code ->
                    let lastSid = conn.LastStreamId |> ValueOption.defaultValue 0u
                    let goAway = Frame.goAway lastSid code
                    do! responseChannel.Writer.WriteAsync(OutboundMessage.Frame goAway, connectionCts.Token)
                    responseChannel.Writer.Complete()
                | StreamError(streamId, code) ->
                    let rst = Frame.reset streamId code
                    do! responseChannel.Writer.WriteAsync(OutboundMessage.Frame rst, connectionCts.Token)
                | StreamClosed sid ->
                    match responders |> Map.tryFind sid with
                    | Some(_, cts) ->
                        use source = cts
                        source.Cancel()
                        responders <- Map.remove sid responders
                    | Option.None -> ()
                | PingRequest bytes ->
                    let pong = Frame.ping bytes |> Frame.withFlags PingFlags.ACK
                    do! responseChannel.Writer.WriteAsync(OutboundMessage.Frame pong, connectionCts.Token)
                | MessageBody(h2s, content) ->
                    let chunkChan = Map.find h2s.Id requestChunks
                    do! chunkChan.Writer.WriteAsync(Chunk.ofBuffer content, connectionCts.Token)

                    match content with
                    | :? IConsumable as streaming ->
                        trace "[DEMUX] waiting for streaming body to be consumed before advancing"
                        do! streaming.Consumed
                    | _ -> ()

                    if h2s.State = HalfClosed then
                        do! chunkChan.Writer.WriteAsync(Chunk.empty, connectionCts.Token)
                        do chunkChan.Writer.Complete()
                        requestChunks <- Map.remove h2s.Id requestChunks
                | HeaderFields(h2s, fields) ->
                    let chunkChan = Channel.CreateUnbounded<Chunk>()

                    if h2s.State = HalfClosed then
                        do chunkChan.Writer.Complete()
                    else
                        requestChunks <- Map.add h2s.Id chunkChan requestChunks

                    match createRequestMessage fields chunkChan.Reader with
                    | ValueNone -> trace "[DEMUX] malformed request"
                    | ValueSome message ->
                        trace $"[DEMUX] starting reply for stream={h2s}"
                        let streamCts = CancellationTokenSource.CreateLinkedTokenSource(connectionCts.Token)
                        let task = replyTo streamCts.Token h2s message
                        responders <- Map.add h2s.Id (task, streamCts) responders
                | TrailerFields(h2s, fields) ->
                    let trailerChunk = fields |> List.map FieldPack.field |> Chunk.ofTrailer

                    let chunkChan = Map.find h2s.Id requestChunks
                    do! chunkChan.Writer.WriteAsync(trailerChunk, connectionCts.Token)

                    if h2s.State = HalfClosed then
                        do! chunkChan.Writer.WriteAsync(Chunk.empty, connectionCts.Token)
                        do chunkChan.Writer.Complete()
                        requestChunks <- Map.remove h2s.Id requestChunks
                | ConnectionClose code -> responseChannel.Writer.Complete()
                | SettingsAck -> ()
                | ClientSettings settings ->
                    let ackFrame = Frame.settings [] |> Frame.withFlags SettingsFlags.ACK
                    do! responseChannel.Writer.WriteAsync(OutboundMessage.Frame ackFrame, connectionCts.Token)
                | DiscardBody(stream, data) -> do ()

            do!
                responders
                |> Map.values
                |> Seq.map (fun struct (task, _) -> task)
                |> Task.WhenAll
        finally
            trace "[DEMUX] === WHILE LOOP EXITED ==="

            try
                trace $"[Cleanup] streamCtx count = {Map.count requestChunks}, inFlight count = {responders.Count}"

                // Complete all pending body channels so in-flight replies can finish
                let clientGone = TaskCanceledException "Client gone"
                do requestChunks |> Map.values |> Seq.iter _.Writer.Complete(clientGone)
                trace "[Cleanup] all pending body channels completed"
            with ex ->
                trace $"[Finally] exception: {ex.Message}"
    }
