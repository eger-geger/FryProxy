namespace FryProxy.IO

open System
open System.IO
open System.Threading.Tasks

/// Readonly sequence of bytes.
type IByteBuffer =

    /// Number of bytes.
    abstract Size: uint64

    /// Asynchronously bytes to a stream.
    abstract WriteAsync: Stream -> ValueTask

/// Readonly bytes stored in memory.
[<Struct>]
type MemoryByteSeq(mem: ReadOnlyMemory<byte>) =

    interface IByteBuffer with
        member _.Size = uint64 mem.Length
        member _.WriteAsync(stream: Stream) = stream.WriteAsync(mem)
