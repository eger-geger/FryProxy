[<AutoOpen>]
module FryProxy.Extension.Stream

open System
open System.IO
open System.Threading
open System.Threading.Tasks

type Stream with

    /// Wait until input is available or operation is cancelled.
    member s.WaitInputAsync cancellationToken : ValueTask =
        ValueTask
        <| task {
            let originalTimeout = s.ReadTimeout
            s.ReadTimeout <- Timeout.Infinite

            try
                let! _ = s.ReadAsync(Array.empty.AsMemory(), cancellationToken)
                ()
            finally
                s.ReadTimeout <- originalTimeout

            return ()
        }

    /// Wait until input is available cancelling after the timeout.
    member s.WaitInputAsync(timeout: TimeSpan) =
        ValueTask
        <| task {
            use cts = new CancellationTokenSource(timeout)
            return! s.WaitInputAsync(cts.Token)
        }
