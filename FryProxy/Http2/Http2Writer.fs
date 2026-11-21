namespace FryProxy.Http2

open System.Diagnostics
open System.IO.Pipelines
open System.Threading.Channels
open System.Threading.Tasks
open FryProxy.Http
open FryProxy.Http2.Frames
open FryProxy.Http2.Hpack

[<Struct; NoComparison; NoEquality>]
type OutboundMessage =
    | Headers of StreamID: StreamId * Fields: Field List
    | Frame of Frame

module Http2Writer =

    let inline private isLast (fh: FrameHeader) =
        (fh.Type = FrameType.HEADERS || fh.Type = FrameType.DATA)
        && FrameHeader.hasFlag FrameFlags.END_STREAM fh

    /// <summary>Serialize outbound HTTP/2 messages to a pipe.</summary>
    /// <remarks>
    /// Header messages are HPACK-encoded using a dynamic table local to this
    /// writer, while frame messages are written directly. The writer is intended
    /// to be used once for one HTTP/2 connection and must not share its message
    /// channel or destination with another writer.
    /// </remarks>
    /// <param name="messages">The reader for outbound messages.</param>
    /// <param name="closedStreams">
    /// The writer for an unbounded channel for this connection. Stream endings
    /// are published before the corresponding frame is encoded.
    /// </param>
    /// <param name="dst">The destination pipe writer.</param>
    /// <returns>
    /// A task that completes when the message channel is completed or the
    /// destination reports a completed or canceled flush.
    /// </returns>
    let run (messages: OutboundMessage ChannelReader) (closedStreams: StreamId ChannelWriter) (dst: PipeWriter) =
        task {
            let mutable interrupted = false
            let mutable fieldTable = Table.empty

            let msgIt = messages.ReadAllAsync().GetAsyncEnumerator()

            let inline advance () =
                if interrupted then
                    ValueTask.FromResult(false)
                else
                    msgIt.MoveNextAsync()

            while! advance () do
                do Trace.TraceInformation($"[WRITER] Processing message {msgIt.Current}")
                
                let! result =
                    match msgIt.Current with
                    | Headers(sid, fields) ->
                        let packedFields = List.map FieldPack.Default fields
                        fieldTable <- Encode.fields sid fieldTable packedFields dst
                        dst.FlushAsync()
                    | Frame frame when isLast frame.Header ->
                        do closedStreams.TryWrite(frame.Header.StreamId) |> ignore
                        do Trace.TraceInformation($"[WRITER] Closed local stream #{frame.Header.StreamId}")
                        Encode.frame frame dst
                    | Frame frame -> Encode.frame frame dst

                if result.IsCompleted || result.IsCanceled then
                    interrupted <- true
        }
