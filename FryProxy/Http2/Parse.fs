module FryProxy.Http2.Parse

open System
open FryProxy.IO.BufferedParser
open FryProxy.Http2

let frameHeader: Parser<FrameHeader> =
    Parser.decoder(fun buff ->
        if buff.Length < 9 then
            None
        else
            Some struct (9us, FrameHeader.decode(buff.Slice(0, 9).Span)))

let padLength (fh: FrameHeader) : byte Parser =
    let padded = fh.Flags &&& 8uy = 8uy

    if padded then
        Parser.pickByte
    else
        Parser.unit 0uy

let dataFrame (fh: FrameHeader) : DataFrame Parser =
    bufferedParser {
        let! padding = padLength fh
        let! data = Parser.bytes(uint64 fh.Length)
        return { PadLength = padding; Data = data }
    }

let headersFrame (fh: FrameHeader) : HeadersFrame Parser =
    bufferedParser {
        let mem = Memory(Array.zeroCreate(int fh.Length))
        let! padding = padLength fh
        let! block = Parser.pickBuffer mem

        return
            { PadLength = padding
              Dependency = 0u
              Exclusive = false
              Weight = 0uy
              FieldBlock = block }
    }
