namespace FryProxy.IO

open System
open System.IO
open System.Threading.Tasks
open Microsoft.FSharp.Core

#nowarn "3391"

exception BufferReadError of err: Exception

/// Allows reading stream in packets and exploring them along the way.
type ReadBuffer(mem: Memory<byte>, src: Stream) =
    let mutable pendingRange = struct (0, 0)

    /// The stream being read
    member val Stream = src

    /// Create another buffered reader using the same memory buffer.
    member _.Share s = ReadBuffer(mem, s)

    /// <summary>
    /// Read-only view of pending bytes.
    /// </summary>
    member _.Pending: byte ReadOnlyMemory =
        match pendingRange with
        | l, r when l = r -> ReadOnlyMemory.Empty
        | l, r -> mem.Slice(l, r - l)

    /// Buffer capacity in bytes.
    member _.Capacity = mem.Length

    /// Number of pending bytes.
    member this.PendingSize = let struct (l, r) = pendingRange in r - l

    /// Move unread bytes to buffer start.
    member private _.Reset() =
        match pendingRange with
        | 0, _ -> ()
        | l, r when l = r -> pendingRange <- (0, 0)
        | l, r ->
            let s = r - l
            mem.Slice(l, s).CopyTo(mem)
            pendingRange <- 0, s

    /// Fill buffer to capacity reading from stream and return number of bytes read from stream.
    member this.Fill() =
        task {
            try
                match this.PendingSize with
                | size when size = mem.Length -> return 0
                | 0 ->
                    let! b = src.ReadAsync(mem)
                    pendingRange <- 0, b
                    return b
                | size ->
                    this.Reset()
                    let! b = src.ReadAsync(mem.Slice(size))
                    pendingRange <- 0, b + size
                    return b
            with err ->
                return raise(BufferReadError err)
        }

    /// Discard given number of initial buffered bytes.
    member this.Discard(n: int) =
        let struct (l, r) = pendingRange

        if n > r - l then
            ArgumentOutOfRangeException(nameof n, n, "Exceeds pending buffer size") |> raise
        else
            pendingRange <- l + n, r
            this.Reset()


    /// Fill the buffer from stream and return readonly view of its content.
    member this.Pick() =
        task {
            let! _ = this.Fill()
            return this.Pending
        }

module ReadBuffer =

    /// Copy and discard given number of bytes through buffer with a given function.
    let inline copy (src: ReadBuffer) (n: uint64) (fn: byte ReadOnlyMemory -> uint64 -> int ValueTask) =
        task {
            let! cp = fn src.Pending n

            let mutable rem = n - uint64 cp
            do src.Discard(cp)

            while rem > 0UL do
                let! buff = src.Pick()
                let! cp = fn buff rem
                do rem <- rem - uint64 cp
                do src.Discard(cp)
        }

    let copyToBuffer src (dst: byte Memory) =
        copy src (uint64 dst.Length)
        <| fun buff m ->
            let mint = int m

            if buff.IsEmpty || mint = 0 then
                ValueTask.FromResult(0)
            elif buff.Length > mint then
                do buff.Slice(0, mint).CopyTo(dst.Slice(dst.Length - mint))
                ValueTask.FromResult(mint)
            else
                do buff.CopyTo(dst.Slice(dst.Length - mint))
                ValueTask.FromResult(buff.Length)

    /// Write pending buffer to destination and proceed with copying remaining source.
    let copyToStream src n (dst: Stream) =
        copy src n
        <| fun buff m ->
            if buff.IsEmpty || m = 0UL then
                ValueTask.FromResult(0)
            elif uint64 buff.Length > m then
                task {
                    do! dst.WriteAsync(buff.Slice(0, int m))
                    return int m
                }
                |> ValueTask<int>
            else
                task {
                    do! dst.WriteAsync(buff)
                    return buff.Length
                }
                |> ValueTask<int>
