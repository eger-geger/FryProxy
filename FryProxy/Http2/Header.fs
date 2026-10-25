namespace rec FryProxy.Http2

open System
open System.Diagnostics

/// Initial 9 octets of every stream carrying stream metadata.
[<Struct; StructuredFormatDisplay("{DisplayText}"); DebuggerDisplay("{DisplayText,nq}")>]
type FrameHeader =
    {
        /// The length of the frame payload expressed as an unsigned 24-bit integer in units of octets.
        /// The 9 octets of the frame header are not included in this value.
        Length: uint32
        /// Determines the format and semantics of the frame.
        Type: FrameType
        /// Boolean flags specific to the frame type.
        Flags: uint8
        /// Monotonically increasing stream identifier unique within a connection scope.
        StreamId: StreamId
    }

    member this.DisplayText = FrameHeader.format this

    override this.ToString() = this.DisplayText


module FrameHeader =

    [<Literal>]
    let StreamIdMask = 0x7fffffffu

    let rec private decodeNum (buf: byte ReadOnlySpan) n acc =
        if n = 0 then
            acc
        else
            let acc' = uint32 (buf[0]) + (acc <<< 8)
            decodeNum (buf.Slice(1)) (n - 1) acc'

    let rec private encodeNum (buf: byte Span) num len =
        if len = 0 then
            ()
        else
            buf[(len - 1)] <- byte num
            encodeNum buf (num >>> 8) (len - 1)

    let decodeFrameType = LanguagePrimitives.EnumOfValue<byte, FrameType>

    let encodeFrameType = LanguagePrimitives.EnumToValue<FrameType, byte>

    let decode (buf: byte ReadOnlySpan) =
        { Length = decodeNum buf 3 0u
          Type = decodeFrameType buf[3]
          Flags = buf[4]
          StreamId = StreamIdMask &&& decodeNum (buf.Slice(5)) 4 0u }


    let encode (fh: FrameHeader) (buf: byte Span) =
        if fh.Length > 0xffffffu then
            invalidArg (nameof fh) $"length exceeds 24 bits: {fh.Length}"

        if fh.StreamId > StreamIdMask then
            invalidArg (nameof fh) $"stream identifier exceeds 31 bits: {fh.StreamId}"

        do encodeNum buf fh.Length 3
        do buf[3] <- encodeFrameType fh.Type
        do buf[4] <- fh.Flags
        do encodeNum (buf.Slice(5)) fh.StreamId 4
        9

    let inline hasFlag flag (fh: FrameHeader) =
        fh.Flags &&& (uint8 flag) = (uint8 flag)

    let frameFlags (fh: FrameHeader) = LanguagePrimitives.EnumOfValue fh.Flags

    let inline private flagsStr<'E when 'E: enum<uint8>> (flags: uint8) =
        if flags = 0uy then
            ""
        else
            (LanguagePrimitives.EnumOfValue<uint8, 'E> flags).ToString().Replace(", ", "|")

    let formatTypeFlags (frameType: FrameType) (flags: uint8) =
        match frameType with
        | FrameType.PING -> flagsStr<PingFlags> flags
        | FrameType.HEADERS -> flagsStr<HeadersFlags> flags
        | FrameType.DATA -> flagsStr<DataFlags> flags
        | FrameType.SETTINGS -> flagsStr<SettingsFlags> flags
        | FrameType.PUSH_PROMISE -> flagsStr<PushPromiseFlags> flags
        | FrameType.CONTINUATION -> flagsStr<ContinuationFlags> flags
        | _ -> if flags = 0uy then "" else $"0x{flags:X2}"

    let format (fh: FrameHeader) =
        let flags = formatTypeFlags fh.Type fh.Flags
        let flagsStr = if String.IsNullOrEmpty flags then "" else $" {flags}"
        $"{fh.Type} {{ #{fh.StreamId}[{fh.Length}]{flagsStr} }}"
