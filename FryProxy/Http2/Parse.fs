module FryProxy.Http2.Parse

open System
open System.Buffers.Binary
open FryProxy.IO.BufferedParser
open FryProxy.Http2
open FryProxy.Http2.Frames

let private preface = "PRI * HTTP/2.0\r\n\r\nSM\r\n\r\n"B

let httpVersion: Parser<struct (uint8 * uint8)> =
    let peekVersion (buff: ReadOnlyMemory<byte>) : struct (uint16 * struct (uint8 * uint8)) voption =
        let span = buff.Span

        if span.StartsWith "PRI"B then
            ValueSome struct (0us, struct (2uy, 0uy)) //TODO: better version representation
        elif
            span.StartsWith "GET"B
            || span.StartsWith "POST"B
            || span.StartsWith "HEAD"B
            || span.StartsWith "PUT"B
            || span.StartsWith "DELETE"B
            || span.StartsWith "CONNECT"B
            || span.StartsWith "OPTIONS"B
            || span.StartsWith "TRACE"B
            || span.StartsWith "PATCH"B
        then
            ValueSome struct (0us, struct (1uy, 1uy))
        else
            ValueNone

    Parser.decoder peekVersion

let connectionPreface: Parser<bool> =
    Parser.decoder (fun buff ->
        if buff.Length < preface.Length then
            ValueNone
        else if buff.Slice(0, preface.Length).Span.SequenceEqual(preface) then
            ValueSome struct (uint16 preface.Length, true)
        else
            ValueSome struct (0us, false))
    |> Parser.commit

let frameHeader: Parser<FrameHeader> =
    Parser.decoder (fun buff ->
        if buff.Length < 9 then
            ValueNone
        else
            ValueSome struct (9us, FrameHeader.decode (buff.Slice(0, 9).Span)))

let paddingAndBodySize (fh: FrameHeader) : (struct (byte * uint * unit Parser)) Parser =
    let padded = FrameHeader.hasFlag FrameFlags.PADDED fh

    if padded then
        Parser.pickByte
        |> Parser.map (fun b -> (b, fh.Length - 1u, Parser.discard (uint64 b)))
    else
        Parser.unit (0uy, fh.Length, Parser.unit ())

let streamId: uint32 Parser =
    Parser.beUint32 |> Parser.map ((&&&) FrameHeader.StreamIdMask)

let dataFrame (fh: FrameHeader) : DataBody Parser =
    bufferedParser {
        let! padLen, bodySize, _ = paddingAndBodySize fh |> Parser.commit
        let! data = Parser.bytes bodySize

        let sliced =
            if padLen = 0uy then
                data
            else
                data.Slice(0, uint64 (bodySize - uint padLen))

        return { PadLength = padLen; Data = sliced }
    }

let priorityFrame: PriorityBody Parser =
    bufferedParser {
        let! first = Parser.beUint32
        let! weight = Parser.pickByte

        let exclusiveBit = first &&& 0x80000000u <> 0u
        let dependency = first &&& FrameHeader.StreamIdMask

        return { Exclusive = exclusiveBit; Dependency = dependency; Weight = weight }
    }

let headersFrame (fh: FrameHeader) : HeadersBody Parser =
    bufferedParser {
        let! padSize, bodySize, padParser = paddingAndBodySize fh |> Parser.commit

        let! priority =
            if FrameHeader.hasFlag HeadersFlags.PRIORITY fh then
                priorityFrame |> Parser.map ValueSome |> Parser.commit
            else
                Parser.unit ValueNone

        let fragmentSize =
            match priority with
            | ValueSome _ -> int bodySize - 5 - int padSize
            | ValueNone -> int bodySize - int padSize

        let mem = Memory(Array.zeroCreate fragmentSize)
        let! block = Parser.pickBuffer mem
        do! padParser

        let exclusive, dependency, weight =
            match priority with
            | ValueSome p -> p.Exclusive, p.Dependency, p.Weight
            | ValueNone -> false, 0u, 0uy

        return
            { PadLength = padSize
              Dependency = dependency
              Exclusive = exclusive
              Weight = weight
              FieldFragment = block }
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
                  { Type = LanguagePrimitives.EnumOfValue(sType); Value = sValue } ]

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
                      { Type = LanguagePrimitives.EnumOfValue(sType); Value = sValue } ]

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
        let! padSize, bodySize, padParser = paddingAndBodySize fh |> Parser.commit

        let! promised = streamId |> Parser.commit

        let blockSize = int bodySize - 4 - int padSize
        let! block = blockSize |> Array.zeroCreate |> Memory |> Parser.pickBuffer
        do! padParser

        return { PadLength = padSize; Promised = promised; FieldBlock = block }
    }

let continuationFrame (fh: FrameHeader) : ContinuationBody Parser =
    bufferedParser {
        let mem = Memory(Array.zeroCreate (int fh.Length))
        let! block = Parser.pickBuffer mem
        return { FieldFragment = block }
    }

let frame: Frame voption Parser =
    bufferedParser {
        let! header = Parser.commit frameHeader

        match header.Type with
        | FrameType.DATA ->
            let! body = dataFrame header |> Parser.commit
            return ValueSome { Header = header; Body = Data body }
        | FrameType.HEADERS ->
            let! body = headersFrame header |> Parser.commit
            return ValueSome { Header = header; Body = Headers body }
        | FrameType.PRIORITY ->
            let! body = priorityFrame |> Parser.commit
            return ValueSome { Header = header; Body = Priority body }
        | FrameType.RST_STREAM ->
            let! body = resetFrame |> Parser.commit
            return ValueSome { Header = header; Body = Reset body }
        | FrameType.SETTINGS ->
            let! body = settingsFrame header |> Parser.commit
            return ValueSome { Header = header; Body = Settings body }
        | FrameType.PUSH_PROMISE ->
            let! body = pushPromiseFrame header |> Parser.commit
            return ValueSome { Header = header; Body = PushPromise body }
        | FrameType.PING ->
            let! body = pingFrame |> Parser.commit
            return ValueSome { Header = header; Body = Ping body }
        | FrameType.GOAWAY ->
            let! body = goAwayFrame header |> Parser.commit
            return ValueSome { Header = header; Body = GoAway body }
        | FrameType.WINDOW_UPDATE ->
            let! body = windowUpdateFrame |> Parser.commit
            return ValueSome { Header = header; Body = WindowUpdate body }
        | FrameType.CONTINUATION ->
            let! body = continuationFrame header |> Parser.commit
            return ValueSome { Header = header; Body = Continuation body }
        | _ ->
            let! _ = Parser.discard (uint64 header.Length)
            return ValueNone
    }
