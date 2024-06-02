namespace FryProxy.IO

open System.Threading.Tasks

type BufferSpan(rb: ReadBuffer, size: uint64) =
    
    let taskQueue = TaskQueue()
    
    member _.EnqueueAfter = taskQueue.Enqueue
    
    interface IByteBuffer with
        member _.Size = size

        member this.WriteAsync stream =
            task {
                do! rb.Copy size stream
                do! taskQueue.AsTask()                
            }
            |> ValueTask
