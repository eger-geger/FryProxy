namespace FryProxy.Http2

open System
open System.Threading
open System.Threading.Tasks
open System.Collections.Generic

module FlowControl =

    [<Literal>]
    let MaxWindowSize = 0x7ffffffful

    [<Literal>]
    let DefaultWindowSize = 0xffff

/// A lock-based HTTP/2 send flow-control window per RFC 9113 §6.9.
///
/// Represents the remaining window credit for a single stream or the connection.
/// The window starts at an initial size and decreases as DATA frames are consumed.
/// It increases when WINDOW_UPDATE frames arrive.
///
/// The window is thread-safe: all mutations are protected by a single lock.
/// When the window is exhausted, <c>ConsumeAsync</c> returns a pending <c>ValueTask</c>
/// that completes once another thread increments the window via <c>TryIncrement</c>
/// or <c>UpdateSize</c>.
///
/// Supports multiple concurrent waiters: each pending consumer gets its own notifier,
/// so cancellation affects only that specific waiter.
type FlowControlWindow(initialSize: int32) =

    let lockObj = obj ()
    let mutable size = initialSize
    let mutable waiters = List.empty<TaskCompletionSource>

    let signalAll () =
        for w in waiters |> List.rev do
            w.TrySetResult() |> ignore

        waiters <- List.empty

    static member Default = FlowControlWindow(initialSize = FlowControl.DefaultWindowSize)

    static member Empty = FlowControlWindow(initialSize = 0)

    /// Attempt to consume window credit for a DATA frame.
    ///
    /// If sufficient credit is available, the window is decremented and returns <c>ValueTask.CompletedTask</c>.
    /// If the window is exhausted, returns a pending <c>ValueTask</c> that completes when credit is restored
    /// by a WINDOW_UPDATE frame (via <c>TryIncrement</c> or <c>TryUpdateSize</c>).
    ///
    /// Per RFC 9113 §6.9.1, the sender MUST NOT send a DATA frame that would cause the
    /// stream or connection window to go negative.
    member _.ConsumeAsync(ask: uint32, ct: CancellationToken) =
        let update () =
            if size <= 0 then
                if ct.IsCancellationRequested then
                    ValueTask.FromCanceled<_> ct
                else
                    let tcs = TaskCompletionSource()

                    if ct.CanBeCanceled then
                        ct.Register(fun () -> tcs.TrySetCanceled(ct) |> ignore) |> ignore

                    waiters <- tcs :: waiters

                    task {
                        do! tcs.Task
                        return 0ul
                    }
                    |> ValueTask<uint32>
            else
                let ack = min size (int32 ask)
                size <- size - ack
                ValueTask.FromResult(uint32 ack)

        lock lockObj update

    /// Repeatedly consume window credit and yield slices of the provided buffer.
    ///
    /// Returns an async enumerable that yields <c>ReadOnlyMemory&lt;byte&gt;</c> slices,
    /// each representing a chunk whose flow control credit has been acknowledged.
    /// If the window is exhausted, the method waits for WINDOW_UPDATE before yielding
    /// the next slice.
    ///
    /// Useful for sending large responses that exceed the current flow control window,
    /// automatically handling backpressure when the window is exhausted.
    member this.SliceAsync(data: ReadOnlyMemory<byte>) : IAsyncEnumerable<ReadOnlyMemory<byte>> =
        { new IAsyncEnumerable<ReadOnlyMemory<byte>> with
            member _.GetAsyncEnumerator(ct: CancellationToken) = SliceEnumerator(this, data, ct) }

    /// Adjust the window size by a signed delta, typically from a peer's SETTINGS_INITIAL_WINDOW_SIZE update.
    ///
    /// Per RFC 9113 §6.9.2, changing SETTINGS_INITIAL_WINDOW_SIZE adjusts all stream windows
    /// by the difference. This method adds the delta to the current window size.
    /// Returns <c>true</c> if the window was updated successfully, <c>false</c> if the new size
    /// would overflow (exceed 2^31-1) or underflow (go below -2^31). Negative windows are allowed
    /// per the specification, but arithmetic underflow is still rejected.
    /// If the window becomes positive after the update, all waiting consumers are signaled.
    member _.TryUpdateSize(delta: int32) =
        let update () =
            let size' = size + delta

            if delta > 0 && size' < size || delta < 0 && size' > size then
                false
            else
                size <- size'

                if size > 0 then
                    signalAll ()

                true

        lock lockObj update


    /// Increment the window by a delta, typically from an incoming WINDOW_UPDATE frame.
    ///
    /// Per RFC 9113 §6.9.1, the WINDOW_UPDATE frame specifies the number of octets
    /// by which the sender increases the window. The increment MUST be greater than 0
    /// and MUST NOT cause the window to exceed 2^31-1.
    ///
    /// Returns <c>true</c> if the window was incremented, <c>false</c> if overflow occurs
    /// (which should be treated as a FLOW_CONTROL_ERROR per RFC 9113 §6.9.2).
    /// If the window becomes positive, all waiting consumers are signaled.
    member _.TryIncrement(delta: uint32) =
        let update () =
            let ws' = size + int32 delta

            if ws' < size then
                false
            else
                size <- ws'

                if ws' > 0 then
                    do signalAll ()

                true

        lock lockObj update

and private SliceEnumerator(window: FlowControlWindow, data: ReadOnlyMemory<byte>, ct: CancellationToken) =
    let mutable offset = 0
    let mutable stopped = false
    let mutable current = ReadOnlyMemory<byte>()

    interface IAsyncEnumerator<ReadOnlyMemory<byte>> with
        member _.MoveNextAsync() =
            if stopped || offset >= data.Length then
                ValueTask.FromResult(false)
            else if ct.IsCancellationRequested then
                Task<_>.FromCanceled<_>(ct) |> ValueTask<bool>
            else
                task {
                    let total = data.Length - offset
                    let mutable credit = 0

                    while credit = 0 do
                        let! ack = window.ConsumeAsync(uint32 total, ct)
                        credit <- int ack

                    current <- data.Slice(offset, credit)
                    offset <- offset + credit
                    return true
                }
                |> ValueTask<bool>

        member _.Current = current

        member _.DisposeAsync() =
            stopped <- true
            ValueTask.CompletedTask
