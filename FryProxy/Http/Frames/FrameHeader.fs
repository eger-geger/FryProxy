namespace FryProxy.Http.Frames

open System

[<Struct>]
type FrameHeader =
    { Length: uint32 // =< 24 bits
      Type: uint8 // TODO: make enum
      Flags: uint8 // type specific flags
      StreamId: uint32 } // =< 31 bits

module FrameHeader =

    [<Literal>]
    let StreamIdMask = 0x7fffffffu

    let rec private decodeNum (buf: byte ReadOnlySpan) n acc =
        if n = 0 then
            acc
        else
            let acc' = uint32(buf[0]) + (acc <<< 8)
            decodeNum (buf.Slice(1)) (n - 1) acc'

    let rec private encodeNum (buf: byte Span) num len =
        if len = 0 then
            ()
        else
            buf[(len - 1)] <- byte num
            encodeNum buf (num >>> 8) (len - 1)

    let decode (buf: byte ReadOnlySpan) =
        if buf.Length <> 9 then
            invalidArg (nameof(buf)) $"invalid header buffer size: {buf.Length}"

        { Length = decodeNum buf 3 0u
          Type = buf[3]
          Flags = buf[4]
          StreamId = StreamIdMask &&& decodeNum (buf.Slice(5)) 4 0u }


    let encode (fh: FrameHeader) (buf: byte Span) =
        if fh.Length > 0xffffffu then
            invalidArg (nameof fh) $"length exceeds 24 bits: {fh.Length}"

        if fh.StreamId > StreamIdMask then
            invalidArg (nameof fh) $"stream identifier exceeds 31 bits: {fh.StreamId}"

        do encodeNum buf fh.Length 3
        do buf[3] <- fh.Type
        do buf[4] <- fh.Flags
        do encodeNum (buf.Slice(5)) fh.StreamId 4
        9
