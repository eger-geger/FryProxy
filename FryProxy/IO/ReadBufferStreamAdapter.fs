namespace FryProxy.IO

open System.IO
open System.Runtime.CompilerServices

/// Stream delegating reads to buffer (reading from buffer first and underlying stream after)
/// and writes to buffered stream.
type ReadBufferStreamAdapter(buff: ReadBuffer) =
    inherit Stream()
    let wrapped = buff.Stream

    override this.Read(buffer, offset, count) =
        this.Read(buffer[offset .. offset + count])

    override _.Read(buffer) = buff.Read(buffer)
    override _.CanRead = wrapped.CanRead
    override _.CanSeek = wrapped.CanSeek
    override _.CanWrite = wrapped.CanWrite
    override _.Length = wrapped.Length
    override _.CanTimeout = wrapped.CanTimeout

    override _.Position
        with get () = wrapped.Position
        and set value = wrapped.Position <- value

    override _.ReadTimeout
        with get () = wrapped.ReadTimeout
        and set value = wrapped.ReadTimeout <- value

    override _.WriteTimeout
        with get () = wrapped.WriteTimeout
        and set value = wrapped.WriteTimeout <- value

    override _.SetLength(value) = wrapped.SetLength(value)
    override _.Seek(offset, origin) = wrapped.Seek(offset, origin)
    override _.Write(buffer) = wrapped.Write(buffer)
    override _.Write(buffer, offset, count) = wrapped.Write(buffer, offset, count)
    override _.WriteByte(value) = wrapped.WriteByte(value)

    override _.WriteAsync(buffer, offset, count, cancellationToken) =
        wrapped.WriteAsync(buffer, offset, count, cancellationToken)

    override _.WriteAsync(buffer, cancellationToken) =
        wrapped.WriteAsync(buffer, cancellationToken)


    override _.BeginWrite(buffer, offset, count, callback, state) =
        wrapped.BeginWrite(buffer, offset, count, callback, state)

    override _.EndWrite(asyncResult) = wrapped.EndWrite(asyncResult)
    override _.Flush() = wrapped.Flush()
    override _.FlushAsync(cancellationToken) = wrapped.FlushAsync(cancellationToken)
    override _.Close() = wrapped.Close()
    override _.Dispose(disposing) = wrapped.Dispose()
    override _.DisposeAsync() = wrapped.DisposeAsync()


type ReadBufferExtensions =

    /// Create a stream delegating reads to buffer and writes to underlying stream.
    [<Extension>]
    static member inline AsStream rb : Stream = new ReadBufferStreamAdapter(rb)
