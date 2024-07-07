namespace FryProxy.IO

open System
open System.IO
open System.Runtime.ExceptionServices
open System.Threading
open System.Threading.Tasks
open FryProxy.Extension

type IOTimeoutException(msg, timeout: TimeSpan, source: Stream) =
    inherit IOException(msg)
    member _.Timeout = timeout
    member _.Stream = source

type ReadTimeoutException(timeout: TimeSpan, source: Stream) =
    inherit IOTimeoutException($"Reading from {source} had timed out after {timeout}ms", timeout, source)

type WriteTimeoutException(timeout: TimeSpan, source: Stream) =
    inherit IOTimeoutException($"Writing to {source} had timed out after {timeout}ms", timeout, source)

module AsyncTimeout =

    let inline timeout error (timeout: TimeSpan) (stream: Stream) ([<InlineIfLambda>] asyncFn) (ct: CancellationToken) =
        if not stream.CanTimeout || timeout <= TimeSpan.Zero then
            asyncFn stream ct
        else
            task {
                use tts = new CancellationTokenSource(timeout)
                use cts = CancellationTokenSource.CreateLinkedTokenSource(tts.Token, ct)

                try
                    return! asyncFn stream cts.Token
                with :? OperationCanceledException as err ->
                    if tts.IsCancellationRequested && not ct.IsCancellationRequested then
                        return error(timeout, stream) |> raise
                    else
                        ExceptionDispatchInfo.Throw(err)
                        return Unchecked.defaultof<_>
            }

    let inline timeoutRead (stream: Stream) =
        timeout ReadTimeoutException (TimeSpan.FromMilliseconds(stream.ReadTimeout)) stream

    let inline timeoutWrite (stream: Stream) =
        timeout WriteTimeoutException (TimeSpan.FromMilliseconds(stream.WriteTimeout)) stream


open AsyncTimeout

/// Raises IOTimeoutException when async read or write duration exceed corresponding timeout.
type AsyncTimeoutDecorator(s: Stream) =
    inherit Stream()

    override _.CanRead = s.CanRead
    override _.CanSeek = s.CanSeek
    override _.CanWrite = s.CanWrite
    override _.Length = s.Length
    override _.Position = s.Position

    override _.Position
        with set value = s.Position <- value

    override this.CanTimeout = s.CanTimeout
    override this.ReadTimeout = s.ReadTimeout

    override this.ReadTimeout
        with set value = s.ReadTimeout <- value

    override this.WriteTimeout = s.WriteTimeout

    override this.WriteTimeout
        with set value = s.WriteTimeout <- value

    override _.Seek(offset, origin) = s.Seek(offset, origin)
    override _.SetLength(value) = s.SetLength(value)

    override _.ReadByte() = s.ReadByte()
    override _.Read(buffer) = s.Read(buffer)
    override _.Read(buffer, offset, count) = s.Read(buffer, offset, count)
    override _.EndRead(asyncResult) = s.EndRead(asyncResult)

    override _.BeginRead(buffer, offset, count, callback, state) =
        s.BeginRead(buffer, offset, count, callback, state)

    override _.Write(buffer) = s.Write(buffer)
    override _.WriteByte(value) = s.WriteByte(value)
    override _.Write(buffer, offset, count) = s.Write(buffer, offset, count)
    override _.EndWrite(asyncResult) = s.EndWrite(asyncResult)

    override _.BeginWrite(buffer, offset, count, callback, state) =
        s.BeginWrite(buffer, offset, count, callback, state)

    override _.Flush() = s.Flush()
    override _.FlushAsync(cancellationToken) = s.FlushAsync(cancellationToken)

    override _.CopyTo(destination, bufferSize) = s.CopyTo(destination, bufferSize)

    override _.ReadAsync(buffer, offset, count, cancellationToken) =
        timeoutRead s
        <| fun s ct -> s.ReadAsync(buffer, offset, count, ct)
        <| cancellationToken

    override _.ReadAsync(buffer, cancellationToken) =
        timeoutRead s
        <| fun s ct -> s.ReadAsync(buffer, ct).AsTask()
        <| cancellationToken
        |> ValueTask.FromTask

    override _.WriteAsync(buffer, offset, count, cancellationToken) =
        timeoutWrite s
        <| fun s ct -> s.WriteAsync(buffer, offset, count, ct).AsUnit()
        <| cancellationToken
        :> Task

    override _.WriteAsync(buffer, cancellationToken) =
        timeoutWrite s
        <| fun s ct -> s.WriteAsync(buffer, ct).AsTask().AsUnit()
        <| cancellationToken
        |> ValueTask

    override _.Close() = s.Close()
    override _.DisposeAsync() = s.DisposeAsync()
