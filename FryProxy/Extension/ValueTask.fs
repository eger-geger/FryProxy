[<AutoOpen>]
module FryProxy.Extension.ValueTask

open System.Threading.Tasks

type ValueTask<'TResult> with

    static member inline FromTask(task: Task<'TResult>) = ValueTask<'TResult>(task)
