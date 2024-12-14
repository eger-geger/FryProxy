module FryProxy.Extension.Stackalloc

#nowarn "9"

open System
open FSharp.NativeInterop

[<Literal>]
let MediumThreshold = 256

/// Allocate array on the stack.
let inline span length =
    let p = NativePtr.stackalloc<'a> length |> NativePtr.toVoidPtr
    Span<'a>(p, length)

/// Allocate array on the stack or heap, depending on size.
let inline medium length =
    if length < MediumThreshold then
        span length
    else
        Span(Array.zeroCreate length)
