namespace FryProxy.Http2

open System.Threading
open System.Threading.Tasks

/// Manages HTTP/2 flow control for outbound DATA frames per RFC 9113 §6.9.
///
/// The FlowController tracks connection-level and per-stream flow-control windows.
/// DATA frames are released to the output only when sufficient window space is available.
type FlowController(initialWindowSize: uint64) =

    [<Literal>]
    static let maxWindow = 0x7fffffffUL

    let windowLock = obj ()
    let mutable initialWindow = initialWindowSize
    let mutable connectionWindow = initialWindowSize
    let mutable streamWindows = Map.empty<StreamId, uint64>
    let mutable pending = Map.empty<StreamId, struct (uint64 * TaskCompletionSource)>

    let readWindow sid =
        Map.tryFind sid streamWindows |> Option.defaultValue initialWindow

    let acquireWindow (cancel: CancellationToken) (sid: StreamId) (amount: uint64) =
        let tryAcquire () =
            let sw = readWindow sid

            if min sw connectionWindow >= amount then
                do connectionWindow <- connectionWindow - amount
                do streamWindows <- Map.add sid (sw - amount) streamWindows
                ValueTask.FromResult(false)
            else if cancel.IsCancellationRequested then
                ValueTask.FromCanceled<bool>(cancel)
            else
                let waiter = TaskCompletionSource()
                let waiterCb = cancel.Register(fun _ -> waiter.SetCanceled(cancel))
                do pending <- Map.add sid (amount, waiter) pending

                task {
                    use _ = waiterCb
                    do! waiter.Task
                    return true
                }
                |> ValueTask<bool>

        task {
            while! lock windowLock tryAcquire do
                ()
        }

    let satisfyPending window struct (request: uint64, waiter: TaskCompletionSource) =
        if request <= window then
            do waiter.SetResult()
            Option.None
        else
            Some struct (request, waiter)

    let tryIncrementConnectionWindow (amount: uint32) : bool =
        let satisfyPendingAll window acc sid struct (request: uint64, waiter: TaskCompletionSource) =
            if request <= window then
                do waiter.SetResult()
                acc
            else
                Map.add sid struct (request, waiter) acc

        let tryUpdate () =
            let newWindow = connectionWindow + uint64 amount

            if newWindow <= maxWindow then
                do connectionWindow <- newWindow
                do pending <- pending |> Map.fold (satisfyPendingAll newWindow) Map.empty
                true
            else
                false

        lock windowLock tryUpdate


    let tryIncrementStreamWindow (sid: StreamId) (amount: uint32) : bool =
        let tryUpdate () =
            let newWindow = readWindow sid + uint64 amount

            if newWindow <= maxWindow then
                streamWindows <- Map.add sid newWindow streamWindows
                pending <- pending |> Map.change sid (Option.bind (satisfyPending newWindow))
                true
            else
                false

        lock windowLock tryUpdate

    let tryIncrementWindowSize (sid: StreamId) (increment: uint32) : bool =
        match sid with
        | 0u -> tryIncrementConnectionWindow increment
        | _ -> tryIncrementStreamWindow sid increment

    let tryUpdateInitialWindowSize (size: uint32) : bool =
        let tryUpdate () =
            let op =
                if uint64 size >= initialWindow then
                    (+) (uint64 size - initialWindow)
                else
                    (-) (initialWindow - uint64 size)

            if uint64 size = initialWindow then
                true
            else if uint64 size > maxWindow then
                false
            else
                let newWindows = streamWindows |> Map.map (fun _ -> op)

                if newWindows |> Map.exists (fun _ w -> w > maxWindow) then
                    false
                else
                    streamWindows <- newWindows
                    initialWindow <- uint64 size

                    let pendingWindows = Map.map (fun sid _ -> readWindow sid) pending

                    for KeyValue(sid, window) in pendingWindows do
                        pending <- pending |> Map.change sid (Option.bind (satisfyPending window))

                    true

        lock windowLock tryUpdate

    /// Increment a flow-control window for connection or stream per RFC 9113 §6.9.1.
    /// Stream ID 0 targets the connection-level window; non-zero targets a specific stream.
    /// Validates that the resulting window would not exceed 2^31-1.
    /// Returns true if the window was increased, false if overflow would occur (FLOW_CONTROL_ERROR).
    member _.TryIncrementWindowSize = tryIncrementWindowSize
    
    /// Try to update the initial flow-control window size per RFC 9113 §6.9.2.
    /// Adjusts all existing stream windows by the difference between the new and old value.
    /// Returns true if updated successfully, false if any stream's window would go negative (FLOW_CONTROL_ERROR).
    member _.TryUpdateInitialWindowSize = tryUpdateInitialWindowSize

    /// Release a stream's flow-control resources when the stream is closed.
    /// Per RFC 9113 §5.1, closing a stream releases its flow-control window.
    /// Cancels any pending wait task and removes the stream from tracking.
    member _.ReleaseStream(sid: StreamId) =
        let cancelPending struct (_: uint64, tcs: TaskCompletionSource) =
            try
                tcs.SetCanceled()
            with _ ->
                ()

            Option.None

        let cleanup () =
            do pending <- Map.change sid (Option.bind cancelPending) pending
            do streamWindows <- Map.remove sid streamWindows

        lock windowLock cleanup

    /// Write a DATA frame through flow control per RFC 9113 §6.9.1.
    /// Waits until both connection-level and stream-level windows have sufficient space.
    member _.WriteData (cancel: CancellationToken) (header: FrameHeader) =
        if header.Length = 0ul then
            ValueTask.CompletedTask
        else
            acquireWindow cancel header.StreamId (uint64 header.Length) |> ValueTask
