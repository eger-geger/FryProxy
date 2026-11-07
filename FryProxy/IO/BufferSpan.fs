namespace FryProxy.IO

open System
open System.Threading
open System.Threading.Tasks

/// Continuous bytes sequence of fixed length backed by buffer.
type BufferSpan
    (rb: ReadBuffer, size: uint64, start: int, length: uint64, completion: TaskCompletionSource, started: ref<bool>) =

    new(rb: ReadBuffer, size: uint64) = BufferSpan(rb, size, 0, size, TaskCompletionSource(), ref false)

    /// Indicates task completing upon consumption.
    member _.Consumed() = completion.Task

    interface IByteBuffer with
        member _.Size = length

        member _.Slice(start: int, length: uint64) =
            BufferSpan(rb, size, start, length, completion, started)

        member this.WriteAsync stream =
            if Interlocked.Exchange(started, true) then
                ValueTask.FromException(InvalidOperationException "Buffer is already being consumed.")
            else
                let copyTask =
                    task {
                        let! prefix = ReadBuffer.discard rb (uint64 start)

                        if prefix <> uint64 start then
                            raise (System.IO.EndOfStreamException $"Discarded {prefix} bytes of {start} before EOF.")

                        let! main = ReadBuffer.copyToStream rb length stream

                        if main <> length then
                            raise (System.IO.EndOfStreamException $"Copied {main} bytes of {length} before EOF.")

                        let! _ = ReadBuffer.discard rb (size - main - prefix)

                        completion.SetResult()
                    }

                copyTask.GetAwaiter().OnCompleted(fun () -> completion.TrySetFromTask(copyTask) |> ignore)

                ValueTask(copyTask)

    interface IConsumable with
        member _.Consumed = completion.Task |> ValueTask
