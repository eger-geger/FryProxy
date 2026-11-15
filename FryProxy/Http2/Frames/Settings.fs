namespace FryProxy.Http2.Frames

open System
open System.Buffers.Binary
open System.Diagnostics

type SettingType =
    /// Maximum size of the compression table used to decode field blocks, in units of octets.
    /// The encoder can select any size equal to or less than this value by using signaling
    /// specific to the compression format inside a field block.
    | HEADER_TABLE_SIZE = 0x01us

    /// Enables or disables server push. A server must not send a PUSH_PROMISE frame if it receives this
    /// parameter set to a value of 0. A client that has both set this parameter to 0 and had it acknowledged
    /// MUST treat the receipt of a PUSH_PROMISE frame as a connection error of type PROTOCOL_ERROR.
    | ENABLE_PUSH = 0x02us

    /// Indicates the maximum number of concurrent streams that the sender will allow.
    /// This limit is directional: it applies to the number of streams that the sender permits the receiver to create.
    /// Endpoints should not treat a value of 0 as special. A zero value does prevent the creation of new
    /// streams; however, this can also happen for any limit exhausted with active streams.
    | MAX_CONCURRENT_STREAMS = 0x03us

    /// Indicates the sender's initial window size (in units of octets) for stream-level flow control.
    /// Affects the window size of all streams. Values above the maximum flow-control window size MUST
    /// be treated as a connection error of type FLOW_CONTROL_ERROR.
    | INITIAL_WINDOW_SIZE = 0x04us

    /// Indicates the size of the largest frame payload that the sender is willing to receive, in units of octets.
    /// The value advertised by an endpoint MUST be between this initial value and the maximum allowed frame size
    /// (16 777 215 octets), inclusive. Values outside this range MUST be treated as a connection error of type
    /// PROTOCOL_ERROR.
    | MAX_FRAME_SIZE = 0x05us

    /// Advisory setting informs a peer of the maximum field section size that the sender is prepared to accept,
    /// in units of octets. The value is based on the uncompressed size of field lines, including the length of
    /// the name and value in units of octets plus an overhead of 32 octets for each field line.
    /// For any given request, a lower limit than what is advertised MAY be enforced.
    | MAX_HEADER_LIST_SIZE = 0x06us

/// Individual setting field contained in SETTINGS frame.
[<Struct; StructuredFormatDisplay("{DisplayText}"); DebuggerDisplay("{DisplayText,nq}")>]
type Setting =
    { Type: SettingType
      Value: uint32 }

    member this.DisplayText = $"{this.Type} = {this.Value}"

    override this.ToString() = this.DisplayText

module Setting =

    /// Creates a HEADER_TABLE_SIZE setting. Specifies the maximum size of the compression table
    /// used to decode field blocks, in units of octets.
    let inline headerTableSize (value: uint32) =
        { Type = SettingType.HEADER_TABLE_SIZE; Value = value }

    /// Creates an ENABLE_PUSH setting. Enables (1) or disables (0) server push.
    /// A value other than 0 or 1 is a PROTOCOL_ERROR per RFC 9113 §6.5.2.
    let inline enablePush (value: uint32) =
        { Type = SettingType.ENABLE_PUSH; Value = value }

    /// Creates a MAX_CONCURRENT_STREAMS setting. Indicates the maximum number of concurrent
    /// streams that the sender will allow. This limit is directional.
    let inline maxConcurrentStreams (value: uint32) =
        { Type = SettingType.MAX_CONCURRENT_STREAMS; Value = value }

    /// Creates an INITIAL_WINDOW_SIZE setting. Indicates the sender's initial window size
    /// (in octets) for stream-level flow control. Values above 2^31-1 are a FLOW_CONTROL_ERROR.
    let inline initialWindowSize (value: uint32) =
        { Type = SettingType.INITIAL_WINDOW_SIZE; Value = value }

    /// Creates a MAX_FRAME_SIZE setting. Indicates the size of the largest frame payload
    /// (in octets) that the sender is willing to receive. Must be between 16384 and 16777215.
    let inline maxFrameSize (value: uint32) =
        { Type = SettingType.MAX_FRAME_SIZE; Value = value }

    /// Creates a MAX_HEADER_LIST_SIZE setting. Advisory setting informing a peer of the
    /// maximum field section size the sender is prepared to accept, in octets.
    let inline maxHeaderListSize (value: uint32) =
        { Type = SettingType.MAX_HEADER_LIST_SIZE; Value = value }

    /// Decodes a single 6-byte setting pair from the HTTP/2 wire format.
    ///
    /// Each setting consists of a 16-bit identifier (big-endian) followed by a 32-bit value (big-endian).
    /// Returns `ValueNone` when the span is not exactly 6 bytes or when the identifier does not correspond
    /// to a recognized `SettingType` (0x01–0x06).
    let decode (buf: byte ReadOnlySpan) =
        if buf.Length <> 6 then
            ValueNone
        else
            let settingType: SettingType =
                BinaryPrimitives.ReadUInt16BigEndian(buf.Slice(0, 2))
                |> LanguagePrimitives.EnumOfValue

            if Enum.IsDefined settingType then
                ValueSome
                    { Type = settingType
                      Value = BinaryPrimitives.ReadUInt32BigEndian(buf.Slice(2, 4)) }
            else
                ValueNone

/// Conveys configuration parameters that affect how endpoints communicate, such as preferences and constraints on
/// peer behavior. Also used to acknowledge the receipt of those settings.
[<Struct; StructuredFormatDisplay("{DisplayText}"); DebuggerDisplay("{DisplayText,nq}")>]
type SettingsBody =
    { Settings: Setting List }

    member this.DisplayText =
        let items = this.Settings |> List.map string |> String.concat "; "
        $"{{ Settings = [{items}] }}"

    override this.ToString() = this.DisplayText

module SettingsBody =

    /// Recursively decodes consecutive 6-byte setting pairs from the span, building an immutable list.
    /// Per RFC 7540 §6.5.2, unrecognized settings are silently ignored and discarded.
    /// Only a span length not divisible by 6 will cause the entire decode to fail.
    [<TailCall>]
    let rec private decodeLoop (buf: byte ReadOnlySpan) offset acc : SettingsBody voption =
        if offset >= buf.Length then
            ValueSome { Settings = List.rev acc }
        else
            let acc' =
                Setting.decode (buf.Slice(offset, 6))
                |> ValueOption.map (fun s -> s :: acc)
                |> ValueOption.defaultValue acc

            decodeLoop buf (offset + 6) acc'

    /// Decodes a SETTINGS frame payload into a `SettingsBody`.
    ///
    /// The span must contain a multiple of 6 bytes, with each 6-byte chunk representing
    /// one setting pair. Returns `ValueNone` when the span length is not a multiple of 6.
    /// Per RFC 7540 §6.5.2, unrecognized setting identifiers are silently ignored and discarded,
    /// so a frame containing unknown settings will still succeed with only the recognized settings.
    /// An empty span (length 0) is valid and produces an empty settings list.
    let decode (buf: byte ReadOnlySpan) : SettingsBody voption =
        if buf.Length % 6 <> 0 then
            ValueNone
        else
            decodeLoop buf 0 []
