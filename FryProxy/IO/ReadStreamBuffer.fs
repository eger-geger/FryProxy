namespace FryProxy.IO

open System
open System.IO
open Microsoft.FSharp.Core

/// <summary>
/// Allows reading stream in packets and exploring them along the way.
/// </summary>
type ReadStreamBuffer(mem: Memory<byte>) =
    let mutable pendingRange = struct (0, 0)

    /// <summary>
    /// Read-only view of pending bytes.
    /// </summary>
    member _.Pending: byte ReadOnlyMemory =
        match pendingRange with
        | l, r when l = r -> ReadOnlyMemory.Empty
        | l, r -> mem.Slice(l, r - l)

    /// <summary>
    /// Buffer capacity in bytes.
    /// </summary>
    member _.Capacity = mem.Length

    /// <summary>
    /// Number of pending bytes.
    /// </summary>
    member this.PendingSize = let struct (l, r) = pendingRange in r - l

    /// <summary>
    /// Move unread bytes to buffer start.
    /// </summary>
    member private _.reset() =
        match pendingRange with
        | 0, _ -> ()
        | l, r when l = r -> pendingRange <- (0, 0)
        | l, r ->
            let s = r - l
            mem.Slice(l, s).CopyTo(mem)
            pendingRange <- 0, s

    /// <summary>
    /// Fill buffer to capacity reading from stream.
    /// </summary>
    /// <returns>Number of bytes read.</returns>
    member this.fill(src: Stream) =
        task {
            match this.PendingSize with
            | size when size = mem.Length -> return 0
            | 0 ->
                let! b = src.ReadAtLeastAsync(mem, mem.Length, false)
                pendingRange <- 0, b
                return b
            | size ->
                this.reset ()
                let! b = src.ReadAtLeastAsync(mem.Slice(size), mem.Length - size, false)
                pendingRange <- 0, b + size
                return b
        }

    /// <summary>
    /// Discard given number of initial buffered bytes.
    /// </summary>
    member _.discard(n: int) =
        let struct (l, r) = pendingRange

        if n > r - l then
            ArgumentOutOfRangeException(nameof n, n, "Exceeds pending buffer size") |> raise
        else
            pendingRange <- l + n, r

    /// <summary>
    /// Fill the destination with buffer content, discarding consumed bytes, and continue reading from stream.
    /// </summary>
    member this.read (src: Stream) (dst: byte Memory) =
        let consumePending l r d =
            mem.Slice(l, d).CopyTo(dst)
            pendingRange <- l + d, r
            this.reset ()

        task {
            let size = this.PendingSize

            match (pendingRange, dst.Length) with
            | (l, r), _ when l = r -> return! src.ReadAsync(dst)
            | (l, r), d when size >= d ->
                consumePending l r d
                return d
            | (l, r), _ ->
                consumePending l r size
                let! b = src.ReadAsync(dst.Slice(size))
                return size + b
        }

    /// <summary>
    /// Access the next byte in stream without advancing (reading), unless the end of stream had been reached.
    /// </summary>
    member this.pickByte(src: Stream) =
        task {
            if this.PendingSize = 0 then
                let! bc = this.fill src
                return if bc = 0 then None else Some(mem.Span[0])
            else
                let struct (l, _) = pendingRange in return Some(mem.Span[l])
        }

    /// <summary>
    /// Fill the buffer from stream and return readonly view of its content.
    /// </summary>
    member this.pickSpan(src: Stream) =
        task {
            let! _ = this.fill src
            return this.Pending
        }

    /// <summary>
    /// Write pending buffer to destination and proceed with copying remaining source.
    /// </summary>
    member this.copy (src: Stream) (dst: Stream) =
        task {
            do! dst.WriteAsync(this.Pending)
            do! src.CopyToAsync(dst)
        }
