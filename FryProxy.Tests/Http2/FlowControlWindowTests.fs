module FryProxy.Tests.Http2.FlowControlWindowTests

open System
open System.Threading
open System.Threading.Tasks
open FryProxy.Http2
open FsUnit
open NUnit.Framework

[<Test; CancelAfter(5000)>]
let ``Full lifecycle`` (cancel: CancellationToken) =
    task {
        let window = FlowControlWindow(1000)

        // Consume 300 + 300 + 300 = 900, window at 100
        let vt1 = window.ConsumeAsync(300u, cancel)
        let vt2 = window.ConsumeAsync(300u, cancel)
        let vt3 = window.ConsumeAsync(300u, cancel)
        vt1.IsCompleted |> should equal true
        vt2.IsCompleted |> should equal true
        vt3.IsCompleted |> should equal true

        // Consume remaining 100, window at 0
        let vt4 = window.ConsumeAsync(100u, cancel)
        vt4.IsCompleted |> should equal true

        let vtPending1 = window.ConsumeAsync(1u, cancel)
        vtPending1.IsCompleted |> should equal false

        window.TryIncrement(50u) |> should equal true
        do! vtPending1

        let vt5 = window.ConsumeAsync(50u, cancel)
        vt5.IsCompleted |> should equal true

        let vtPending2 = window.ConsumeAsync(1u, cancel)
        vtPending2.IsCompleted |> should equal false
        window.UpdateSize(200) // delta: 0 + 200 = 200
        do! vtPending2

        let vt6 = window.ConsumeAsync(200u, cancel)
        vt6.IsCompleted |> should equal true

        let vtPending3 = window.ConsumeAsync(1u, cancel)
        vtPending3.IsCompleted |> should equal false
        window.TryIncrement(10u) |> should equal true
        do! vtPending3

        let vt7 = window.ConsumeAsync(10u, cancel)
        vt7.IsCompleted |> should equal true
    }

[<Test; CancelAfter(5000)>]
let ``UpdateSize and negative window`` (cancel: CancellationToken) =
    task {
        let window = FlowControlWindow(0)

        // Zero/negative delta should not signal
        let vt = window.ConsumeAsync(1u, cancel)
        vt.IsCompleted |> should equal false

        window.UpdateSize(0) // delta 0: size stays 0
        do! Task.Delay(100)
        vt.IsCompleted |> should equal false

        window.UpdateSize(-10) // delta -10: size becomes -10
        do! Task.Delay(100)
        vt.IsCompleted |> should equal false

        window.UpdateSize(110) // delta 110: -10 + 110 = 100
        do! vt

        let vtConsume = window.ConsumeAsync(100u, cancel)
        vtConsume.IsCompleted |> should equal true

        window.UpdateSize(-1) // delta -1: 0 - 1 = -1
        let vtPending = window.ConsumeAsync(1u, cancel)
        vtPending.IsCompleted |> should equal false

        window.TryIncrement(1u) |> should equal true
        vtPending.IsCompleted |> should equal false

        window.TryIncrement(1u) |> should equal true
        do! vtPending
    }

[<Test; CancelAfter(5000)>]
let ``Overflow recovery`` (cancel: CancellationToken) =
    task {
        let window = FlowControlWindow(0)

        window.UpdateSize(Int32.MaxValue) // delta: 0 + MaxValue = MaxValue
        window.TryIncrement(1u) |> should equal false
        window.TryIncrement(1000u) |> should equal false

        window.UpdateSize(-Int32.MaxValue + 65535) // delta: MaxValue + (-MaxValue + 65535) = 65535
        let vt1 = window.ConsumeAsync(65535u, cancel)
        vt1.IsCompleted |> should equal true

        let vtPending = window.ConsumeAsync(1u, cancel)
        vtPending.IsCompleted |> should equal false
        window.TryIncrement(100u) |> should equal true
        do! vtPending

        let vt2 = window.ConsumeAsync(100u, cancel)
        vt2.IsCompleted |> should equal true
    }

[<Test; CancelAfter(5000)>]
let ``Concurrent access`` (cancel: CancellationToken) =
    task {
        let window = FlowControlWindow(10000)

        // 10 concurrent consumers each take 1000 bytes
        let consumeTasks =
            [ 1..10 ] |> List.map (fun _ -> window.ConsumeAsync(1000u, cancel).AsTask())

        do! Task.WhenAll(Array.ofList consumeTasks)

        // Window exhausted — pending consume signaled from main thread
        let vtPending = window.ConsumeAsync(1u, cancel)
        vtPending.IsCompleted |> should equal false
        window.TryIncrement(100u) |> should equal true
        do! vtPending

        let vtFinal = window.ConsumeAsync(100u, cancel)
        vtFinal.IsCompleted |> should equal true
    }

[<Test; CancelAfter(5000)>]
let ``Multiple waiters with cancellation`` (cancel: CancellationToken) =
    task {
        let window = FlowControlWindow(100)

        let vt1 = window.ConsumeAsync(100u, cancel)
        vt1.IsCompleted |> should equal true

        // Two waiters: only the first is cancellable
        use cts = CancellationTokenSource.CreateLinkedTokenSource(cancel)
        let vtPending1 = window.ConsumeAsync(1u, cts.Token)
        let vtPending2 = window.ConsumeAsync(1u, cancel)
        vtPending1.IsCompleted |> should equal false
        vtPending2.IsCompleted |> should equal false

        // Cancelling one waiter doesn't affect the other
        cts.Cancel()
        vtPending1.IsCanceled |> should equal true
        vtPending2.IsCompleted |> should equal false

        window.TryIncrement(100u) |> should equal true
        do! vtPending2

        let vtFinal = window.ConsumeAsync(99u, cancel)
        vtFinal.IsCompleted |> should equal true
    }

[<Test; CancelAfter(5000)>]
let ``Already cancelled token`` (cancel: CancellationToken) =
    task {
        let window = FlowControlWindow(100)
        use cts = CancellationTokenSource.CreateLinkedTokenSource(cancel)
        cts.Cancel()

        // Credit available — consume succeeds even with canceled token
        let vt1 = window.ConsumeAsync(100u, cts.Token)
        vt1.IsCompleted |> should equal true

        // Window exhausted with canceled token — returns canceled immediately
        let vtCanceled = window.ConsumeAsync(1u, cts.Token)
        vtCanceled.IsCompleted |> should equal true
        vtCanceled.IsCanceled |> should equal true

        // Window recovers after canceled
        window.TryIncrement(10u) |> should equal true
        let vtRecover = window.ConsumeAsync(10u, cancel)
        vtRecover.IsCompleted |> should equal true
    }

[<Test; CancelAfter(5000)>]
let ``Cancel during wait`` (cancel: CancellationToken) =
    task {
        let window = FlowControlWindow(100)

        let vt1 = window.ConsumeAsync(100u, cancel)
        vt1.IsCompleted |> should equal true

        // Pending consume canceled mid-wait
        use cts = CancellationTokenSource.CreateLinkedTokenSource cancel
        let vtPending = window.ConsumeAsync(1u, cts.Token)
        vtPending.IsCompleted |> should equal false
        cts.Cancel()
        vtPending.IsCanceled |> should equal true

        // Window recovers after cancellation
        window.TryIncrement(10u) |> should equal true
        let vtRecover = window.ConsumeAsync(10u, cancel)
        vtRecover.IsCompleted |> should equal true
    }
