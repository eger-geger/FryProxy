namespace FryProxy.IO.BufferedParser

open System
open System.Collections.Generic
open System.Threading
open System.Threading.Tasks
open FryProxy.Extension
open FryProxy.IO

type LazySeqGen<'S, 'R> = 'S voption -> struct ('S * 'R) voption Parser

type LazyIter<'S, 'R>(gen: LazySeqGen<'S, 'R>, rb: ReadBuffer, init: ParseState) =

    let completed = TaskCompletionSource()
    let mutable parserState = init
    let mutable generatorState = ValueNone
    let mutable current = Unchecked.defaultof<struct ('S * 'R)>

    let next () =
        task {
            try
                match! gen generatorState (rb, parserState) with
                | ps, ValueSome(s, r) ->
                    current <- struct (s, r)
                    parserState <- ps
                    generatorState <- ValueSome s
                    return true
                | _, ValueNone ->
                    current <- Unchecked.defaultof<struct ('S * 'R)>
                    do ignore <| completed.TrySetResult()
                    return false
            with
            | :? OperationCanceledException as err ->
                do ignore <| completed.TrySetCanceled(err.CancellationToken)
                return err.Rethrow()
            | err ->
                do ignore <| completed.TrySetException(err)
                return err.Rethrow()
        }

    member _.Advance() =
        if completed.Task.IsCompleted then
            ValueTask.FromResult(false)
        else
            ValueTask<bool>(next ())

    member _.Current = current

    member this.ToEnumerator(ct: CancellationToken) =
        { new IAsyncEnumerator<_> with
            override _.MoveNextAsync() =
                if ct.IsCancellationRequested then
                    ValueTask.FromCanceled<bool>(ct)
                else
                    this.Advance()

            override _.Current = this.Current

          interface IAsyncDisposable with
              override _.DisposeAsync() =
                  do ignore <| completed.TrySetResult()
                  ValueTask.CompletedTask }

    member this.ToEnumerable() =
        { new IAsyncEnumerable<_> with
            override _.GetAsyncEnumerator ct = this.ToEnumerator ct }

    interface IConsumable with
        member _.Consumed = completed.Task |> ValueTask
