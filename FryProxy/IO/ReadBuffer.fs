namespace FryProxy.IO

open System
open System.IO
open Microsoft.FSharp.Core

/// Allows reading stream in packets and exploring them along the way.
type ReadBuffer(mem: Memory<byte>) =
    let mutable pendingRange = struct (0, 0)

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

    /// <summary>
    /// Fill buffer to capacity reading from stream.
    /// </summary>
    /// <returns>Number of bytes read.</returns>
    member this.Fill(src: Stream) =
        task {
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
        }

    /// Discard given number of initial buffered bytes.
    member this.Discard(n: int) =
        let struct (l, r) = pendingRange

        if n > r - l then
            ArgumentOutOfRangeException(nameof n, n, "Exceeds pending buffer size") |> raise
        else
            pendingRange <- l + n, r
            this.Reset()

    /// Fill the destination with buffer content, discarding consumed bytes, and continue reading from stream.
    member this.Read (src: Stream) (dst: byte Memory) =
        let consumePending l r d =
            mem.Slice(l, d).CopyTo(dst)
            pendingRange <- l + d, r
            this.Reset()

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

    /// Fill the buffer from stream and return readonly view of its content.
    member this.PickSpan(src: Stream) =
        task {
            let! _ = this.Fill src
            return this.Pending
        }

    /// Write pending buffer to destination and proceed with copying remaining source.
    member this.Copy (src: Stream) (dst: Stream) =
        task {
            do! dst.WriteAsync(this.Pending)
            pendingRange <- 0, 0
            do! src.CopyToAsync(dst)
        }
