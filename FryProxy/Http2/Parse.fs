module FryProxy.Http2.Parse

open System
open System.Buffers.Binary
open FryProxy.Http2.Frames
open FryProxy.IO.BufferedParser
open FryProxy.Http2


let frameHeader: Parser<FrameHeader> =
    Parser.decoder (fun buff ->
        if buff.Length < 9 then
            ValueNone
        else
            ValueSome struct (9us, FrameHeader.decode (buff.Slice(0, 9).Span)))

let padLength (fh: FrameHeader) : (struct (byte * uint)) Parser =
    let padded = fh.Flags &&& 8uy = 8uy

    if padded then
        Parser.pickByte |> Parser.map (fun b -> (b, fh.Length - 1u))
    else
        Parser.unit (0uy, fh.Length)

let streamId: uint32 Parser =
    Parser.beUint32 |> Parser.map ((&&&) FrameHeader.StreamIdMask)

let dataFrame (fh: FrameHeader) : DataBody Parser =
    bufferedParser {
        let! padLen, bodySize = padLength fh |> Parser.commit
        let! data = Parser.bytes bodySize
        return { PadLength = padLen; Data = data }
    }

let headersFrame (fh: FrameHeader) : HeadersBody Parser =
    bufferedParser {
        let! padLen, bodySize = padLength fh |> Parser.commit
        let mem = Memory(Array.zeroCreate (int bodySize))
        let! block = Parser.pickBuffer mem

        return
            { PadLength = padLen
              Dependency = 0u
              Exclusive = false
              Weight = 0uy
              FieldFragment = block }
    }

let priorityFrame: PriorityBody Parser =
    bufferedParser {
        let! first = Parser.beUint32
        let! weight = Parser.pickByte

        let exclusiveBit = first &&& 0x80000000u <> 0u
        let dependency = first &&& FrameHeader.StreamIdMask

        return { Exclusive = exclusiveBit; Dependency = dependency; Weight = weight }
    }


let resetFrame: ResetBody Parser =
    Parser.beUint32
    |> Parser.map LanguagePrimitives.EnumOfValue
    |> Parser.map (fun code -> { ErrorCode = code })


let tryDecodeSettings (bytes: ReadOnlyMemory<byte>) =
    if bytes.Length % 6 <> 0 then
        ValueNone
    else
        let settings =
            [ for i in 0 .. bytes.Length / 6 - 1 do
                  let slice = bytes.Span.Slice(i * 6, 6)
                  let sType = BinaryPrimitives.ReadUInt16BigEndian(slice.Slice(0, 2))
                  let sValue = BinaryPrimitives.ReadUInt32BigEndian(slice.Slice(2, 4))
                  Setting(LanguagePrimitives.EnumOfValue(sType), sValue) ]

        ValueSome settings

let settingsFrame (fh: FrameHeader) : SettingsBody Parser =
    Parser.decoder (fun buf ->
        if uint buf.Length < fh.Length then
            ValueNone
        else
            let settings =
                [ for i in 0 .. int (fh.Length / 6u) - 1 do
                      let slice = buf.Span.Slice(i * 6, 6)
                      let sType = BinaryPrimitives.ReadUInt16BigEndian(slice.Slice(0, 2))
                      let sValue = BinaryPrimitives.ReadUInt32BigEndian(slice.Slice(2, 4))
                      Setting(LanguagePrimitives.EnumOfValue(sType), sValue) ]

            ValueSome struct (uint16 fh.Length, { Settings = settings }))

let pingFrame: PingBody Parser =
    bufferedParser {
        let mem = Memory(Array.zeroCreate 8)
        let! data = Parser.pickBuffer mem
        return { Data = data }
    }

let windowUpdateFrame: WindowUpdateBody Parser =
    Parser.beUint32
    |> Parser.map ((&&&) 0x7fffffffu)
    |> Parser.map (fun i -> { Increment = i })

let goAwayFrame (fh: FrameHeader) : GoAwayBody Parser =
    bufferedParser {
        let! last = streamId |> Parser.commit
        let! errCode = Parser.beUint32 |> Parser.commit
        let! debugData = Parser.bytes (fh.Length - 8u)

        return
            { Last = last
              ErrorCode = LanguagePrimitives.EnumOfValue errCode
              DebugData = debugData }
    }

let pushPromiseFrame (fh: FrameHeader) : PushPromiseBody Parser =
    bufferedParser {
        let! padLen, bodySize = padLength fh |> Parser.commit

        let! promised = streamId |> Parser.commit

        let! block = int bodySize - 4 |> Array.zeroCreate |> Memory |> Parser.pickBuffer

        return { PadLength = padLen; Promised = promised; FieldBlock = block }
    }

let continuationFrame (fh: FrameHeader) : ContinuationBody Parser =
    bufferedParser {
        let mem = Memory(Array.zeroCreate (int fh.Length))
        let! block = Parser.pickBuffer mem
        return { FieldFragment = block }
    }

let frame: Frame Parser =
    bufferedParser {
        let! header = Parser.commit frameHeader

        match header.Type with
        | FrameType.DATA ->
            let! body = dataFrame header |> Parser.commit
            return { Header = header; Body = Data body }
        | FrameType.HEADERS ->
            let! body = headersFrame header |> Parser.commit
            return { Header = header; Body = Headers body }
        | FrameType.PRIORITY ->
            let! body = priorityFrame |> Parser.commit
            return { Header = header; Body = Priority body }
        | FrameType.RST_STREAM ->
            let! body = resetFrame |> Parser.commit
            return { Header = header; Body = Reset body }
        | FrameType.SETTINGS ->
            let! body = settingsFrame header |> Parser.commit
            return { Header = header; Body = Settings body }
        | FrameType.PUSH_PROMISE ->
            let! body = pushPromiseFrame header |> Parser.commit
            return { Header = header; Body = PushPromise body }
        | FrameType.PING ->
            let! body = pingFrame |> Parser.commit
            return { Header = header; Body = Ping body }
        | FrameType.GOAWAY ->
            let! body = goAwayFrame header |> Parser.commit
            return { Header = header; Body = GoAway body }
        | FrameType.WINDOW_UPDATE ->
            let! body = windowUpdateFrame |> Parser.commit
            return { Header = header; Body = WindowUpdate body }
        | FrameType.CONTINUATION ->
            let! body = continuationFrame header |> Parser.commit
            return { Header = header; Body = Continuation body }
        | _ -> return! Parser.failed "Unknown frame type"
    }
