module FryProxy.Extension.Stackalloc

#nowarn "9"

open System
open FSharp.NativeInterop

/// Allocate array on the stack.
let inline span length =
    let p = NativePtr.stackalloc<'a> length |> NativePtr.toVoidPtr
    Span<'a>(p, length)
