namespace FryProxy.Tests.Constraints

open System
open System.Threading.Tasks
open NUnit.Framework

type ThrowAsync(errT: Type) =

    member _.From task =
        Assert.ThrowsAsync(errT, (fun () -> task)) |> ignore

    member this.From(task: ValueTask) = this.From(task.AsTask())
    member this.From(task: ValueTask<'a>) = this.From(task.AsTask())
