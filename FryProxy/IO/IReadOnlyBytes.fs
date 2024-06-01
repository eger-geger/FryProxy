namespace FryProxy.IO

open System
open System.IO
open System.Threading.Tasks

/// Readonly sequence of bytes.
type IReadOnlyBytes =

    /// Asynchronously copy given amount of bytes to a stream.
    abstract CopyAsync: uint64 * Stream -> ValueTask

/// Readonly bytes stored in memory.
[<Struct>]
type ReadOnlyMemoryBytes(mem: ReadOnlyMemory<byte>) =

    interface IReadOnlyBytes with
        member _.CopyAsync(n: uint64, s: Stream) = s.WriteAsync(mem.Slice(0, int n))
