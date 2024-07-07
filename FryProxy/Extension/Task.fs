[<AutoOpen>]
module FryProxy.Extension.Task

open System.Threading.Tasks

type ValueTask<'TResult> with

    static member inline FromTask(task: Task<'TResult>) = ValueTask<'TResult>(task)

type Task with

    member inline this.AsUnit() =
        task {
            do! this
            return ()
        }
