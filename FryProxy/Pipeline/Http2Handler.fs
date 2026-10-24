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
    let _, rem = TryPop<Scheme> rem

    match struct (method, authority, path) with
    | Some({ Method = m }, _), Some({ Authority = a }, _), Some({ Path = p }, _) when m <> HttpMethod.Connect ->
        ValueSome({ Method = m; Target = p; Version = Protocol.Http2 }, FieldOf { Host = a } :: rem)
    | Some({ Method = m }, _), Some({ Authority = a }, _), Option.None when m = HttpMethod.Connect ->
        ValueSome({ Method = m; Target = a; Version = Protocol.Http2 }, FieldOf { Host = a } :: rem)
    | Some({ Method = m }, _), Some({ Authority = a }, _), Option.None when m = HttpMethod.Options ->
        ValueSome({ Method = m; Target = "*"; Version = Protocol.Http2 }, FieldOf { Host = a } :: rem)
    | _ -> ValueNone

let createRequestMessage (fields: FieldPack List) chunks : RequestMessage ValueOption =
    let rawFields = fields |> List.map FieldPack.field

    rawFields
    |> tryDecodeRequestLine
    |> ValueOption.map (fun (requestLine, reqFields) ->
        { Header = { StartLine = requestLine; Fields = reqFields }; Body = Empty })

let private reply
    (pipeline: 'ctx RequestHandler)
    (streamId: StreamId)
    (req: RequestMessage)
    (chan: ChannelWriter<OutboundMessage>)
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

        // Send initial server SETTINGS frame per RFC 9113
        let initialSettings = Frame.settings []
        do! responseChannel.Writer.WriteAsync(OutboundMessage.Frame initialSettings)

        let! frameSeq = Parser.unfold tryParseConnectionFrame |> Parser.run clientBuffer
        let frameEnum = frameSeq.GetAsyncEnumerator()
        let chunksChan = Channel.CreateUnbounded<Chunk>()

        while! frameEnum.MoveNextAsync() do
            let struct (conn, transition) = frameEnum.Current
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
            | PingRequest bytes -> eprintfn $"ping request: %A{bytes}"
            | MessageBody(sid, stream) ->
                let chunk: Chunk =
                    { Header = { Size = stream.Size; Extensions = [] }; Body = Content(stream) }

                do! chunksChan.Writer.WriteAsync(chunk)
            | MessageFields(sid, fields) ->
                match createRequestMessage fields (chunksChan.Reader.ReadAllAsync()) with
                | ValueNone -> eprintfn "malformed request"
                | ValueSome message -> do! reply pipeline sid message responseChannel.Writer
            | StreamReset(sid, code) -> eprintfn $"stream reset: %A{code}"
            | ConnectionClose code ->
                responseChannel.Writer.Complete()
            | ConnectionWindowUpdate increment -> eprintfn $"connection window update: %d{increment}"
            | StreamWindowUpdate(streamId, increment) -> eprintfn $"stream window update: %d{increment}"
            | SettingsAck -> do ()
            | ClientSettings settings ->
                let ackFrame = Frame.settings [] |> Frame.withFlags SettingsFlags.ACK
                do! responseChannel.Writer.WriteAsync(OutboundMessage.Frame ackFrame)
    }
