namespace FryProxy

open System
open System.Collections.Generic
open System.Threading.Tasks

/// Stack of disposable resources released in LIFO order.
type ResourceStack(items: IDisposable IEnumerable) =

    let stack = Stack<IDisposable>(items)

    let disposeAsync (d: IDisposable) =
        match d with
        | :? IAsyncDisposable as a -> a.DisposeAsync()
        | _ -> let _ = d.Dispose() in ValueTask.CompletedTask

    new() = new ResourceStack(List.empty)

    member _.Push<'T when 'T :> IDisposable>(d: 'T) : 'T = let _ = stack.Push(d) in d

    interface IAsyncDisposable with
        override _.DisposeAsync() =
            ValueTask
            <| task {
                let mutable errors = List.empty

                for d in stack do
                    try
                        do! disposeAsync d
                    with err ->
                        errors <- err :: errors

                if not errors.IsEmpty then
                    raise(AggregateException(errors))
            }

    interface IDisposable with
        override this.Dispose() =
            (this :> IAsyncDisposable).DisposeAsync().GetAwaiter().GetResult()
