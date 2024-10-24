namespace FryProxy.IO

open System
open System.Collections
open System.IO
open System.Threading.Tasks

/// Readonly sequence of bytes.
type IByteBuffer =

    /// Number of bytes.
    abstract Size: uint64

    /// Asynchronously write bytes to a stream.
    abstract WriteAsync: Stream -> ValueTask

/// Readonly bytes stored in memory.
[<Struct; CustomEquality; CustomComparison>]
type MemoryByteSeq(mem: ReadOnlyMemory<byte>) =

    member _.Memory = mem

    override _.Equals(obj) =
        match obj with
        | :? MemoryByteSeq as bs -> mem.Span.SequenceEqual(bs.Memory.Span)
        | _ -> false

    override _.GetHashCode() =
        mem.Slice(0, min 10 mem.Length).ToArray()
        |> Array.fold (fun a b -> HashCode.Combine(a, int b)) mem.Length

    interface IComparable with
        member this.CompareTo(obj) =
            match obj with
            | :? MemoryByteSeq as bs -> mem.Span.SequenceCompareTo(bs.Memory.Span)
            | _ -> invalidArg (nameof obj) $"{obj} is not {typeof<MemoryByteSeq>}"

    interface IByteBuffer with
        member _.Size = uint64 mem.Length
        member _.WriteAsync(stream: Stream) = stream.WriteAsync(mem)
