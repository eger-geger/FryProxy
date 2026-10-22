namespace FryProxy.Http2

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

    /// Serialize outbound HTTP/2 messages to a pipe.
    ///
    /// Header messages are HPACK-encoded using a dynamic table local to this
    /// writer, while frame messages are written directly. The returned task
    /// completes when the channel is completed or the destination reports a
    /// completed or canceled flush.
    ///
    /// The writer is intended to be used once for one HTTP/2 connection and
    /// must not share its message channel or destination with another writer.
    let run (messages: ChannelReader<OutboundMessage>) (dst: PipeWriter) =
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
                let! result =
                    match msgIt.Current with
                    | Headers(sid, fields) ->
                        let packedFields = List.map FieldPack.Default fields
                        fieldTable <- Encode.fields sid fieldTable packedFields dst
                        dst.FlushAsync()
                    | Frame frame -> Encode.frame frame dst

                if result.IsCompleted || result.IsCanceled then
                    interrupted <- true
        }
