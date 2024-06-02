namespace FryProxy.IO

open System.Threading.Tasks

type BufferSpan(rb: ReadBuffer, size: uint64) =

    let mutable consumed = false

    member _.Consumed() = consumed

    interface IByteBuffer with
        member _.Size = size

        member this.WriteAsync stream =
            task {
                do! rb.Copy size stream
                consumed <- true
            }
            |> ValueTask
