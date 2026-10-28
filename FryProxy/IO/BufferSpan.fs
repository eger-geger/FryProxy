namespace FryProxy.IO

open System
open System.Threading
open System.Threading.Tasks

/// Continuous bytes sequence of fixed length backed by buffer.
type BufferSpan(rb: ReadBuffer, size: uint64) =

    let completion = TaskCompletionSource()
    let mutable started = 0uy

    /// Indicates task completing upon consumption.
    member _.Consumed() = completion.Task

    interface IByteBuffer with
        member _.Size = size

        member this.WriteAsync stream =
            if Interlocked.Exchange(&started, 1uy) <> 0uy then
                ValueTask.FromException(InvalidOperationException "Buffer has been consumed.")
            else
                let copyTask =
                    task {
                        let! n = ReadBuffer.copyToStream rb size stream

                        if n <> size then
                            raise (System.IO.EndOfStreamException $"Copied {n} bytes of {size} before EOF.")

                        completion.SetResult()
                    }

                copyTask.GetAwaiter().OnCompleted(fun () -> completion.TrySetFromTask(copyTask) |> ignore)

                ValueTask(copyTask)

    interface IConsumable with
        member _.Consumed = completion.Task |> ValueTask
