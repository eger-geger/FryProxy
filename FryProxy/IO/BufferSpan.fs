namespace FryProxy.IO

open System.Threading.Tasks

/// Continuous bytes sequence of fixed length backed by buffer.
type BufferSpan(rb: ReadBuffer, size: uint64) =

    let mutable consumed = false
    
    /// Indicates whether all bytes had been read (copied). 
    member _.Consumed() = consumed

    interface IByteBuffer with
        member _.Size = size

        member this.WriteAsync stream =
            if consumed then
                ValueTask.CompletedTask
            else
                ValueTask
                <| task {
                    do! rb.Copy size stream
                    consumed <- true
                }

    interface IConsumable with
        member _.Consumed = consumed
