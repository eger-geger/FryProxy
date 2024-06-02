namespace FryProxy.IO

open System.Collections.Generic
open System.Threading.Tasks

type TaskQueue() =

    let mutable queue = Queue<ValueTask Lazy>()

    member _.Enqueue = queue.Enqueue

    member _.AsTask() =
        task {
            for task in queue do
                do! task.Value
        }
