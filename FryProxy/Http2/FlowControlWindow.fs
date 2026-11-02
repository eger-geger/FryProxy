namespace FryProxy.Http2

open System.Threading
open System.Threading.Tasks

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

    static member Default = FlowControlWindow(initialSize = 65535)

    static member Empty = FlowControlWindow(initialSize = 0)

    /// Attempt to consume window credit for a DATA frame.
    ///
    /// If sufficient credit is available, the window is decremented and returns <c>ValueTask.CompletedTask</c>.
    /// If the window is exhausted, returns a pending <c>ValueTask</c> that completes when credit is restored
    /// by a WINDOW_UPDATE frame (via <c>TryIncrement</c> or <c>UpdateSize</c>).
    ///
    /// Per RFC 9113 §6.9.1, the sender MUST NOT send a DATA frame that would cause the
    /// stream or connection window to go negative.
    member _.ConsumeAsync(ask: uint32, cancel: CancellationToken) =
        let update () =
            if size < 0 || uint32 size < ask then
                if cancel.IsCancellationRequested then
                    ValueTask.FromCanceled cancel
                else
                    let tcs = TaskCompletionSource()

                    if cancel.CanBeCanceled then
                        cancel.Register(fun () -> tcs.TrySetCanceled(cancel) |> ignore) |> ignore

                    waiters <- tcs :: waiters
                    ValueTask(tcs.Task)
            else
                size <- size - int32 ask
                ValueTask.CompletedTask

        lock lockObj update


    /// Set the absolute window size, typically from a peer's SETTINGS_INITIAL_WINDOW_SIZE update.
    ///
    /// Per RFC 9113 §6.9.2, changing SETTINGS_INITIAL_WINDOW_SIZE adjusts all stream windows
    /// by the difference. This method sets the window to an explicit value.
    /// If the new size is positive and the window was previously exhausted, all waiting consumers are signaled.
    member _.UpdateSize(newSize: int32) =
        let update () =
            size <- newSize

            if newSize > 0 then
                signalAll ()

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
