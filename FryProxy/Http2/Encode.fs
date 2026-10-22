module FryProxy.Http2.Encode

open System.Buffers
open System.Buffers.Binary
open System.IO.Pipelines
open System.Threading.Tasks
open FryProxy.Http2.Frames
open FryProxy.Http2.Hpack

[<Literal>]
let DefaultFieldFrameBufferSize = 16384

let inline writeUint32 value (dest: byte IBufferWriter) =
    BinaryPrimitives.WriteUInt32BigEndian(dest.GetSpan(4), uint32 value)
    dest.Advance(4)

let inline writeByte value (dest: byte IBufferWriter) =
    dest.GetSpan(1).Item(0) <- value
    dest.Advance(1)

let inline writeUint16 value (dest: byte IBufferWriter) =
    BinaryPrimitives.WriteUInt16BigEndian(dest.GetSpan(2), value)
    dest.Advance(2)

let inline frameHeader (fh: FrameHeader) (writer: byte IBufferWriter) =
    writer.Advance(FrameHeader.encode fh (writer.GetSpan(9)))

let inline private priority
    (body: ^T when ^T: (member Exclusive: bool) and ^T: (member Dependency: StreamId) and ^T: (member Weight: uint8))
    (bw: byte IBufferWriter)
    =
    let dep =
        if body.Exclusive then
            0x80000000u ||| body.Dependency
        else
            body.Dependency

    writeUint32 dep bw
    writeByte body.Weight bw

let inline private padded
    (writer: ^T -> PipeWriter -> ValueTask<FlushResult>)
    (header: FrameHeader)
    (body: ^T when ^T: (member PadLength: uint8))
    (pw: PipeWriter)
    =
    if FrameHeader.hasFlag HeadersFlags.PADDED header then
        task {
            let padLen = body.PadLength

            do writeByte padLen pw

            let! fr = writer body pw

            if not (fr.IsCompleted || fr.IsCanceled) && padLen > 0uy then
                let span = pw.GetSpan(int padLen)
                span.Slice(0, int padLen).Clear()
                pw.Advance(int padLen)
                return! pw.FlushAsync()
            else
                return fr
        }
        |> ValueTask<FlushResult>
    else
        writer body pw

let inline private goAway (body: GoAwayBody) (bw: PipeWriter) =
    task {
        do writeUint32 body.Last bw
        do writeUint32 (uint32 body.ErrorCode) bw
        do! body.DebugData.WriteAsync(bw.AsStream())
        return! bw.FlushAsync()
    }
    |> ValueTask<FlushResult>

let inline private pushPromise (body: PushPromiseBody) (bw: PipeWriter) =
    do writeUint32 body.Promised bw
    bw.WriteAsync body.FieldBlock

let inline private settings (body: SettingsBody) (bw: byte IBufferWriter) =
    for Setting(sType, sVal) in body.Settings do
        do writeUint16 (uint16 sType) bw
        do writeUint32 sVal bw

let inline private data (body: DataBody) (bw: PipeWriter) =
    task {
        do! body.Data.WriteAsync(bw.AsStream())
        return! bw.FlushAsync()
    }
    |> ValueTask<FlushResult>

let inline private headers (header: FrameHeader) (body: HeadersBody) (bw: PipeWriter) =
    if FrameHeader.hasFlag HeadersFlags.PRIORITY header then
        priority body bw

    bw.WriteAsync body.FieldFragment

let inline private flushed (writer: ^T -> byte IBufferWriter -> unit) (value: ^T) (bw: PipeWriter) =
    writer value bw
    bw.FlushAsync()

/// Write frame to a stream.
let frame (frame: Frame) (pw: PipeWriter) =
    task {
        frameHeader frame.Header pw

        match frame.Body with
        | Headers body -> return! padded (headers frame.Header) frame.Header body pw
        | Data body -> return! padded data frame.Header body pw
        | Priority body -> return! flushed priority body pw
        | Reset body -> return! flushed writeUint32 (uint32 body.ErrorCode) pw
        | Settings body -> return! flushed settings body pw
        | PushPromise body -> return! padded pushPromise frame.Header body pw
        | Ping body -> return! pw.WriteAsync body.Data
        | GoAway body -> return! goAway body pw
        | WindowUpdate body -> return! flushed writeUint32 body.Increment pw
        | Continuation c -> return! pw.WriteAsync c.FieldFragment
    }
    |> ValueTask<FlushResult>

/// <summary>
/// Encodes header fields into the provided buffer writer as HTTP/2 frames (an initial HEADERS frame
/// followed by CONTINUATION frames if necessary until all fields are encoded), updating the HPACK dynamic table state.
/// </summary>
/// <param name="streamId">The stream identifier to associate with the generated frames.</param>
/// <param name="table">The initial <see cref="DynamicTable"/> state before encoding the header fields.</param>
/// <param name="fields">The list of <see cref="FieldPack"/> items to encode into HPACK representation.</param>
/// <param name="bw">The destination buffer writer to which frame headers and payloads are written.</param>
/// <returns>The updated <see cref="DynamicTable"/> state after encoding all header fields.</returns>
let fields (streamId: StreamId) (table: DynamicTable) (fields: FieldPack list) (bw: byte IBufferWriter) : DynamicTable =
    let rec loop frameType tbl remainingFields =
        let span = (bw.GetSpan DefaultFieldFrameBufferSize).Slice(0, DefaultFieldFrameBufferSize)

        let struct (written, table', overflow) =
            Table.encodeFields tbl remainingFields (span.Slice(9))

        let flags =
            match overflow with
            | [] -> uint8 HeadersFlags.END_HEADERS
            | _ -> 0uy

        let fh =
            { Length = uint32 written
              Type = frameType
              Flags = flags
              StreamId = streamId }

        FrameHeader.encode fh (span.Slice(0, 9)) |> ignore
        bw.Advance(9 + written)

        if List.isEmpty overflow then
            table'
        else
            loop FrameType.CONTINUATION table' overflow

    loop FrameType.HEADERS table fields
