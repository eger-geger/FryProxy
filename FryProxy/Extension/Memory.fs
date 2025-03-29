[<AutoOpen>]
module FryProxy.Extension.Memory

open System
open System.Buffers

type 'a Memory with
    member mem.NoopManager() =
        { new IMemoryOwner<'a> with
            member _.Memory = mem
            member _.Dispose() = () }
