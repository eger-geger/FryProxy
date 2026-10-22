namespace FryProxy.Http2

open System.IO.Pipelines
open System.Threading.Channels
open System.Threading.Tasks
open FryProxy.Http
open FryProxy.Http2.Frames
open FryProxy.Http2.Hpack
open FryProxy.IO

[<Struct; NoComparison; NoEquality>]
type OutputMessage =
    | FieldsMessage of StreamID: StreamId * Fields: Field List
    | FrameMessage of Frame

module Responses =
    
    // let private encodeMessage (msg: OutputMessage) (dst: PipeWriter) =
    //     task {
    //         match msg with
    //         | FieldsMessage(streamId, fields) ->
    //             
    //         | FrameMessage frame -> return! Encode.frame frame dst
    //     }
    
    let private responseWriterLoop (messages: ChannelReader<OutputMessage>) (dst: PipeWriter) =
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
                match msgIt.Current with
                | FieldsMessage (streamId, fields) -> failwith "not implemented"
                | FrameMessage frame ->


                let msg = msgIt.Current
                let! result = Encode.frame Unchecked.defaultof<Frame> dst

                if result.IsCompleted || result.IsCanceled then
                    interrupted <- true

            do! dst.CompleteAsync()
        }