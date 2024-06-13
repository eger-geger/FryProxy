namespace FryProxy

open System
open System.Collections.Generic
open System.Threading.Tasks

/// Stack of disposable resources released in LIFO order.
type ResourceStack(items: IAsyncDisposable IEnumerable) =

    let stack = Stack<IAsyncDisposable>(items)

    new() = ResourceStack(List.empty)

    member _.Push(a) = stack.Push(a)

    member _.Push(d: IDisposable) =
        stack.Push(
            { new IAsyncDisposable with
                override _.DisposeAsync() =
                    d.Dispose()
                    ValueTask.CompletedTask }
        )

    interface IAsyncDisposable with
        override _.DisposeAsync() =
            ValueTask
            <| task {
                let mutable errors = List.empty

                for disposable in stack do
                    try
                        do! disposable.DisposeAsync()
                    with err ->
                        errors <- err :: errors

                if not errors.IsEmpty then
                    raise(AggregateException(errors))
            }
