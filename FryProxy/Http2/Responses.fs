namespace FryProxy.Http2

open System.IO.Pipelines
open System.Threading.Channels
open System.Threading.Tasks
open FryProxy.Http
open FryProxy.Http2.Frames
open FryProxy.Http2.Hpack
open FryProxy.IO

[<Struct; NoComparison; NoEquality>]
type OutputResponse =
    | FieldResponse of StreamID: StreamId * Fields: Field List
    | ContentResponse of StreamID: StreamId * Body: IByteBuffer

module Responses =
    
    
    let private responseWriterLoop (messages: ChannelReader<OutputResponse>) (dst: PipeWriter) =
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