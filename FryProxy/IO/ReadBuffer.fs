namespace FryProxy.IO

open System
open System.IO
open Microsoft.FSharp.Core

#nowarn "3391"

exception BufferReadError of err: Exception

/// Allows reading stream in packets and exploring them along the way.
type ReadBuffer(mem: Memory<byte>, src: Stream) =
    let mutable pendingRange = struct (0, 0)

    /// The stream bing read
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

    /// Write pending buffer to destination and proceed with copying remaining source.
    member this.Copy (n: uint64) (dst: Stream) =
        let copyFromBuffer (buff: byte ReadOnlyMemory) (n: uint64) =
            task {
                if buff.IsEmpty || n = 0UL then
                    return 0UL
                elif uint64 buff.Length > n then
                    do! dst.WriteAsync(buff.Slice(0, int n))
                    this.Discard(int n)
                    return n
                else
                    do! dst.WriteAsync(buff)
                    this.Discard(buff.Length)
                    return uint64 buff.Length
            }

        task {
            let! cp = copyFromBuffer this.Pending n

            let mutable remaining = n - cp

            while remaining > 0UL do
                let! _ = this.Fill()
                let! cp = copyFromBuffer this.Pending remaining
                remaining <- remaining - cp
        }
