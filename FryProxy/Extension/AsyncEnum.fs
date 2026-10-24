[<AutoOpen>]
module FryProxy.Extension.AsyncEnum

open System.Collections.Generic
open System.Threading

type private MappingAsyncEnumerator<'a, 'b>(source: 'a IAsyncEnumerator, mapping: 'a -> 'b) =
    interface IAsyncEnumerator<'b> with
        member _.MoveNextAsync() = source.MoveNextAsync()
        member _.Current = mapping source.Current
        member _.DisposeAsync() = source.DisposeAsync()

type AsyncEnumExtensions =

    [<System.Runtime.CompilerServices.Extension>]
    static member Map(this: IAsyncEnumerable<'a>, mapping: 'a -> 'b) : 'b IAsyncEnumerable =
        { new IAsyncEnumerable<'b> with
            member _.GetAsyncEnumerator cancellationToken =
                new MappingAsyncEnumerator<'a, 'b>(this.GetAsyncEnumerator cancellationToken, mapping) }

    [<System.Runtime.CompilerServices.Extension>]
    static member ToList(this: IAsyncEnumerable<'a>, ?cancellationToken: CancellationToken) =
        let ct = defaultArg cancellationToken CancellationToken.None

        task {
            let enum = this.GetAsyncEnumerator(ct)
            let mutable items = []

            while! enum.MoveNextAsync() do
                items <- enum.Current :: items

            return List.rev items
        }
