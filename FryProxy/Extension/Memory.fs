[<AutoOpen>]
module FryProxy.Extension.Memory

open System
open System.Buffers

#nowarn "3391"

type 'a Memory with

    /// Wrap allocated memory into owner interface.
    member mem.UnitOwner() =
        { new IMemoryOwner<'a> with
            member _.Memory = mem
            member _.Dispose() = () }

type 'a IMemoryOwner with

    /// Restrict managed memory size.
    member this.Strict(size) =
        { new IMemoryOwner<'a> with
            member _.Memory = this.Memory.Slice(0, size)
            member _.Dispose() = this.Dispose() }

type 'a MemoryPool with

    /// Returns a managed memory block of exact size.
    member this.Strict(size) = this.Rent(size).Strict(size)
