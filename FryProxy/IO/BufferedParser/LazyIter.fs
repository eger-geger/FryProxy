namespace FryProxy.IO.BufferedParser

open System
open System.Collections.Generic
open System.Threading
open System.Threading.Tasks
open FryProxy.IO

type 'T LazySeqGen = 'T voption -> 'T voption Parser

type 'T LazyIter(gen: 'T LazySeqGen, rb: ReadBuffer, init: ParseState) =

    let tokens = List<CancellationToken>()
    let mutable consumed = false
    let mutable parserState = init
    let mutable generatorState = ValueNone
    let mutable current = Unchecked.defaultof<'T>

    let next () =
        task {
            match! gen generatorState (rb, parserState) with
            | ps, ValueSome x ->
                current <- x
                parserState <- ps
                generatorState <- ValueSome x
                return true
            | _, ValueNone ->
                current <- Unchecked.defaultof<'T>
                consumed <- true
                return false
        }

    member this.WithToken(token: CancellationToken) =
        tokens.Add(token)
        this

    interface 'T IAsyncEnumerator with
        override this.MoveNextAsync() =
            let cancelledToken = tokens |> Seq.filter(_.IsCancellationRequested) |> Seq.tryHead

            if cancelledToken.IsSome then
                ValueTask.FromCanceled<bool>(cancelledToken.Value)
            elif consumed then
                ValueTask.FromResult(false)
            else
                ValueTask<bool>(next())

        override this.Current = current

    interface IAsyncDisposable with
        override this.DisposeAsync() = ValueTask.CompletedTask

    interface IConsumable with
        member _.Consumed = consumed
