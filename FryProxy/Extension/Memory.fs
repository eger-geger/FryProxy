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

[<Struct>]
type 'a SizedBuffer(buffer: IMemoryOwner<'a>, size: int) =

    /// Restricted memory size.
    member _.Size = size

    /// Wrapped buffer size.
    member _.Length = buffer.Memory.Length

    /// Copy bytes from the buffer to the destination memory.
    member _.CopyTo(dest: Memory<'a>) =
        buffer.Memory.Slice(0, size).CopyTo(dest)

    // Writes suffix to the end of the buffer and returns updated sized buffer.
    member _.Append(suffix: _ ReadOnlyMemory) =
        do suffix.CopyTo(buffer.Memory.Slice(size))
        new SizedBuffer<'a>(buffer, size + suffix.Length)

    /// Returns an empty buffer.
    static member Empty = SizedBuffer.From(Memory.Empty)

    /// Wraps a given memory buffer.
    static member From(buffer: Memory<'a>) =
        new SizedBuffer<_>(buffer.UnitOwner(), buffer.Length)

    interface 'a IMemoryOwner with
        member _.Memory = buffer.Memory.Slice(0, size)
        member _.Dispose() = buffer.Dispose()

type 'a MemoryPool with

    /// Append suffix to the end of the buffer and returns updated sized buffer.
    /// Allocates a new memory buffer if it is too small.
    member this.Append (buffer: 'a SizedBuffer) (suffix: 'a ReadOnlyMemory) =
        let total = buffer.Size + suffix.Length

        if buffer.Length >= total then
            buffer.Append suffix
        else
            use old = buffer
            let buf = this.Rent(total)

            do old.CopyTo(buf.Memory)
            do suffix.CopyTo(buf.Memory.Slice(old.Size))

            new SizedBuffer<'a>(buf, total)
