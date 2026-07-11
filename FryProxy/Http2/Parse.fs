module FryProxy.Http2.Parse

open System
open FryProxy.Http2.Frames
open FryProxy.IO.BufferedParser
open FryProxy.Http2

let frameHeader: Parser<FrameHeader> =
    Parser.decoder (fun buff ->
        if buff.Length < 9 then
            ValueNone
        else
            ValueSome struct (9us, FrameHeader.decode (buff.Slice(0, 9).Span)))

let padLength (fh: FrameHeader) : byte Parser =
    let padded = fh.Flags &&& 8uy = 8uy

    if padded then Parser.pickByte else Parser.unit 0uy

let dataFrame (fh: FrameHeader) : DataBody Parser =
    bufferedParser {
        let! padding = padLength fh
        let mem = Memory(Array.zeroCreate (int fh.Length))
        let! data = Parser.pickBuffer mem
        return { PadLength = padding; Data = data }
    }

let headersFrame (fh: FrameHeader) : HeadersBody Parser =
    bufferedParser {
        let mem = Memory(Array.zeroCreate (int fh.Length))
        let! padding = padLength fh |> Parser.commit
        let! block = Parser.pickBuffer mem

        return
            { PadLength = padding
              Dependency = 0u
              Exclusive = false
              Weight = 0uy
              FieldFragment = block }
    }

let frame: Frame Parser =
    bufferedParser {
        let! header = Parser.commit frameHeader

        match header.Type with
        | FrameType.HEADERS ->
            let! body = headersFrame header |> Parser.commit
            return { Header = header; Body = Headers body }
        | FrameType.DATA ->
            let! body = dataFrame header |> Parser.commit
            return { Header = header; Body = Data body }
        | _ -> return! Parser.failed "Unknown frame type"
    }
