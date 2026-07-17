namespace FryProxy.Http2.Frames

open System

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
[<Struct>]
type Setting = Setting of SettingType * uint32



[<Flags>]
type SettingsFlags =
    /// Upon receiving a SETTINGS frame with the ACK flag set, the sender of the altered settings
    /// can rely on the values from the oldest unacknowledged SETTINGS frame having been applied.
    | ACK = 0x01uy

/// Conveys configuration parameters that affect how endpoints communicate, such as preferences and constraints on
/// peer behavior. Also used to acknowledge the receipt of those settings.
[<Struct>]
type SettingsBody =
    { Settings: Setting List }
