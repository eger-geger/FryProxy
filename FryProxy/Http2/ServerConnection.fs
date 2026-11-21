namespace FryProxy.Http2

open System
open System.Buffers
open System.Diagnostics
open FryProxy.Extension
open FryProxy.Http2.Frames
open FryProxy.Http2.FrameFlags
open FryProxy.Http2.Hpack

type FieldKind =
    | Header
    | Trailer

/// Incomplete header or trailer data transmitted on a given stream.
[<Struct; CustomEquality; NoComparison>]
type PendingFields =
    internal
        { StreamId: StreamId
          Buffer: byte SizedBuffer
          Kind: FieldKind }

    interface IEquatable<PendingFields> with
        member this.Equals(other: PendingFields) =
            this.Kind = other.Kind
            && this.StreamId = other.StreamId
            && this.Buffer.Size = other.Buffer.Size
            && this.Buffer.Span.SequenceEqual(other.Buffer.Span)


[<Struct; CustomEquality; CustomComparison>]
[<StructuredFormatDisplay("{DisplayText}"); DebuggerDisplay("{DisplayText,nq}")>]
type H2Stream =
    internal
        { Id: StreamId
          State: StreamState
          Window: FlowControlWindow }


    /// Creates a new stream in idle state.
    static member Idle sid =
        { Id = sid; State = StreamState.Idle; Window = FlowControlWindow.Default }

    /// Creates a new stream in closed state.
    static member Closed sid =
        { Id = sid; State = StreamState.Closed; Window = FlowControlWindow.Empty }

    /// Creates a new stream in open state.
    static member Open sid =
        { Id = sid; State = StreamState.Open; Window = FlowControlWindow.Default }

    /// Creates a new stream in half-closed state.
    static member HalfClosedRemote sid =
        { Id = sid
          State = StreamState.HalfClosedRemote
          Window = FlowControlWindow.Empty }

    /// Creates a new stream in locally half-closed state.
    static member HalfClosedLocal sid =
        { Id = sid
          State = StreamState.HalfClosedLocal
          Window = FlowControlWindow.Default }

    member this.DisplayText = $"{{ Id = #{this.Id}; State = {this.State} }}"

    override this.ToString() = this.DisplayText

    interface IComparable<H2Stream> with
        member this.CompareTo(other) =
            match compare this.Id other.Id with
            | 0 -> compare this.State other.State
            | r -> r

    interface IEquatable<H2Stream> with
        member this.Equals(other: H2Stream) : bool =
            this.Id = other.Id && this.State = other.State

    override this.Equals(obj: obj) =
        match obj with
        | :? H2Stream as other -> (this :> IEquatable<H2Stream>).Equals(other)
        | _ -> false

    override this.GetHashCode() = hash (this.Id, this.State)

/// Represents a connection to a single HTTP/2 client.
[<Struct; CustomEquality; NoComparison>]
type ConnectionState =
    internal
        {
            /// Lowest possible identifier for the next active stream.
            /// Incremented by 2 after each new stream is opened to enforce stream multiplexing ordering.
            NextStreamId: StreamId
            /// Identifier of the last stream to be processed before shutting down the connection.
            /// Set when a GOAWAY frame is received; streams with IDs greater than this are refused.
            LastStreamId: StreamId voption
            /// Active streams keyed by stream ID.
            /// Contains streams in states other than IDLE and CLOSED.
            ActiveStreams: Map<StreamId, H2Stream>
            /// Identifiers of the recently reset streams.
            /// DATA frames sent on those streams will be ignored without error.
            ResetStreams: StreamId Set
            /// HPack dynamic table state for header compression and decompression.
            HPackTable: DynamicTable
            /// Incomplete header or trailer fields awaiting a CONTINUATION frame.
            PendingFields: PendingFields voption
            /// Connection-level flow control window governing the total DATA bytes accepted across all streams.
            Window: FlowControlWindow
            /// Initial flow control window size for newly opened streams.
            /// Updated via <see cref="SettingType.INITIAL_WINDOW_SIZE"/> SETTINGS frames.
            /// When changed, the delta is applied to all existing active streams per RFC 9113 §6.9.2.
            InitialWindowSize: int
        }

    interface IEquatable<ConnectionState> with
        member this.Equals(other: ConnectionState) =
            this.NextStreamId = other.NextStreamId
            && this.ActiveStreams = other.ActiveStreams
            && this.ResetStreams = other.ResetStreams
            && this.HPackTable = other.HPackTable
            && this.PendingFields = other.PendingFields


module ConnectionState =

    let withPendingFields kind streamId buffer (conn: ConnectionState) =
        { conn with
            PendingFields = ValueSome { StreamId = streamId; Buffer = buffer; Kind = kind } }

    let withPendingHeader streamId buffer =
        withPendingFields Header streamId buffer

    let withPendingTrailer streamId buffer =
        withPendingFields Trailer streamId buffer


[<Struct>]
type TransitionResult =
    | None
    | ConnectionError of Code: ErrorCode
    | StreamError of StreamId: StreamId * Code: ErrorCode
    | StreamClosed of StreamId: StreamId
    | PingRequest of Bytes: byte ReadOnlyMemory
    | MessageBody of Stream: H2Stream * Data: Octets
    | DiscardBody of Stream: H2Stream * Data: Octets
    | HeaderFields of Stream: H2Stream * Fields: FieldPack List
    | TrailerFields of Stream: H2Stream * Fields: FieldPack List
    | ConnectionClose of Code: ErrorCode
    | SettingsAck
    | ClientSettings of Settings: Setting List

type ConnectionStateTransition = (struct (ConnectionState * TransitionResult))

module Transition =
    let inline connectionError code conn : ConnectionStateTransition = conn, ConnectionError code

    let inline streamError code sid conn : ConnectionStateTransition = conn, StreamError(sid, code)

    let inline streamRefused sid conn =
        streamError ErrorCode.REFUSED_STREAM sid conn

    let inline ping data cnx : ConnectionStateTransition = cnx, PingRequest data

    let inline close code cnx : ConnectionStateTransition = cnx, ConnectionClose code

    let inline clientSettings settings cnx : ConnectionStateTransition = cnx, ClientSettings settings

    let inline streamClosed sid cnx : ConnectionStateTransition = cnx, StreamClosed sid

    let inline settingsAck cnx : ConnectionStateTransition = cnx, SettingsAck

    let inline content h2s data cnx : ConnectionStateTransition = cnx, MessageBody(h2s, data)

    let inline discard h2s data cnx : ConnectionStateTransition = cnx, DiscardBody(h2s, data)

    let inline headerFields h2s fields cnx : ConnectionStateTransition = cnx, HeaderFields(h2s, fields)

    let inline trailerFields h2s fields cnx : ConnectionStateTransition = cnx, TrailerFields(h2s, fields)

    let inline fields kind =
        match kind with
        | Header -> headerFields
        | Trailer -> trailerFields

    let inline pending conn : ConnectionStateTransition = conn, None

module ServerConnection =

    /// Look up a stream by ID in the connection state.
    let inline private findStream (conn: ConnectionState) id =
        if id >= conn.NextStreamId then
            H2Stream.Idle id
        else
            Map.tryFind id conn.ActiveStreams |> Option.defaultValue (H2Stream.Closed id)

    let inline private addStream h2s (conn: ConnectionState) =
        { conn with
            NextStreamId = h2s.Id + 2u
            ActiveStreams = conn.ActiveStreams |> Map.add h2s.Id h2s }

    let inline private isRefused sid conn =
        conn.LastStreamId |> ValueOption.map ((>) sid) |> ValueOption.defaultValue false

    let decodeFieldsFrame
        (kind: FieldKind)
        (fh: FrameHeader)
        body
        (h2s: H2Stream)
        (conn: ConnectionState)
        : ConnectionStateTransition =
        let fieldBuff =
            conn.PendingFields
            |> ValueOption.map _.Buffer
            |> ValueOption.defaultValue SizedBuffer.Empty
            |> MemoryPool.Shared.Append
            <| body

        if FrameHeader.hasFlag END_HEADERS fh then
            match Table.decodeFields conn.HPackTable fieldBuff.Span with
            | Ok(fields, table) ->
                { conn with PendingFields = ValueNone; HPackTable = table }
                |> Transition.fields kind h2s fields
            | Error _ -> Transition.connectionError ErrorCode.COMPRESSION_ERROR conn
        else
            conn
            |> ConnectionState.withPendingFields kind h2s.Id fieldBuff
            |> Transition.pending

    let inline private closeRemoteStream
        (fh: FrameHeader)
        (h2s: H2Stream)
        (conn: ConnectionState)
        : struct (H2Stream * ConnectionState) =
        let isLastFrame = FrameHeader.hasFlag END_STREAM fh

        match struct (isLastFrame, h2s.State) with
        | true, Closed -> h2s, conn
        | true, HalfClosedLocal ->
            let active = Map.remove h2s.Id conn.ActiveStreams
            { h2s with State = Closed }, { conn with ActiveStreams = active }
        | true, _ ->
            let h2s' = { h2s with State = HalfClosedRemote }
            let active = Map.add h2s.Id h2s' conn.ActiveStreams
            h2s', { conn with ActiveStreams = active }
        | _ -> h2s, conn

    let closeLocalStream sid (conn: ConnectionState) =
        match Map.tryFind sid conn.ActiveStreams with
        | Some h2s when h2s.State = Open ->
            let h2s' = { h2s with State = HalfClosedLocal }
            let active = conn.ActiveStreams |> Map.add sid h2s'
            { conn with ActiveStreams = active }
        | Some h2s when h2s.State = HalfClosedRemote -> { conn with ActiveStreams = Map.remove sid conn.ActiveStreams }
        | _ -> conn

    let inline private resetStream errorCode (h2s: H2Stream) conn =
        if h2s.State = StreamState.Closed then
            Transition.pending conn
        else
            { conn with
                ResetStreams = conn.ResetStreams.Add h2s.Id
                ActiveStreams = conn.ActiveStreams.Remove h2s.Id }
            |> Transition.streamError errorCode h2s.Id

    let (|ResettableState|_|) state =
        match state with
        | Open
        | Closed
        | Reserved
        | HalfClosedRemote
        | HalfClosedLocal -> true
        | _ -> false

    let inline acknowledgeSettings (body: SettingsBody) =
        if List.isEmpty body.Settings then
            Transition.settingsAck
        else
            Transition.connectionError ErrorCode.FRAME_SIZE_ERROR

    /// Validate and update the initial window size per RFC 9113 §6.9.2.
    ///
    /// Returns ``Error FLOW_CONTROL_ERROR`` if the new value exceeds the maximum
    /// flow-control window size (2^31-1).
    let inline private updateInitialWindowSize (value: uint32) (conn: ConnectionState) =
        if value > FlowControl.MaxWindowSize then
            Error ErrorCode.FLOW_CONTROL_ERROR
        else
            let delta = int32 value - conn.InitialWindowSize

            let accepted =
                conn.ActiveStreams |> Map.forall (fun _ h2s -> h2s.Window.TryUpdateSize delta)

            if accepted then
                Ok { conn with InitialWindowSize = int value }
            else
                Error ErrorCode.FLOW_CONTROL_ERROR

    [<TailCall>]
    let rec private updateSettingLoop (settings: Setting list) (conn: ConnectionState) =
        match settings with
        | [] -> Ok conn
        | s :: tail ->
            let res =
                match s.Type with
                | SettingType.INITIAL_WINDOW_SIZE -> updateInitialWindowSize s.Value conn
                | SettingType.ENABLE_PUSH when s.Value > 1u -> Error ErrorCode.PROTOCOL_ERROR
                | SettingType.MAX_FRAME_SIZE when s.Value < FrameSize.Min -> Error ErrorCode.PROTOCOL_ERROR
                | SettingType.MAX_FRAME_SIZE when s.Value > FrameSize.Max -> Error ErrorCode.PROTOCOL_ERROR
                | _ -> Ok conn

            match res with
            | Ok conn' -> updateSettingLoop tail conn'
            | err -> err

    let inline private updateSettings settings conn =
        match updateSettingLoop settings conn with
        | Ok conn' -> Transition.clientSettings settings conn'
        | Error code -> Transition.connectionError code conn

    let inline private updateConnectionWindow increment conn =
        if increment = 0u then
            Transition.connectionError ErrorCode.PROTOCOL_ERROR conn
        else if not (conn.Window.TryIncrement increment) then
            Transition.connectionError ErrorCode.FLOW_CONTROL_ERROR conn
        else
            Transition.pending conn

    let inline private updateStreamWindow (h2s: H2Stream) increment conn =
        if isRefused h2s.Id conn then
            Transition.pending conn
        elif increment = 0u then
            resetStream ErrorCode.PROTOCOL_ERROR h2s conn
        else if not (h2s.Window.TryIncrement increment) then
            resetStream ErrorCode.FLOW_CONTROL_ERROR h2s conn
        else
            Transition.pending conn

    let inline private closeStream (h2s: H2Stream) (conn: ConnectionState) =
        if h2s.State = StreamState.Closed then
            Transition.pending conn
        else
            { conn with ActiveStreams = conn.ActiveStreams.Remove h2s.Id }
            |> Transition.streamClosed h2s.Id

    let inline private openStream (fh: FrameHeader) (hb: HeadersBody) (conn: ConnectionState) =
        let state =
            if FrameHeader.hasFlag END_STREAM fh then
                HalfClosedRemote
            else
                Open

        let h2s =
            { Id = fh.StreamId
              State = state
              Window = FlowControlWindow(conn.InitialWindowSize) }

        conn |> addStream h2s |> decodeFieldsFrame Header fh hb.FieldFragment h2s

    let inline private protocolError conn =
        Transition.connectionError ErrorCode.PROTOCOL_ERROR conn

    /// HTTP/2 connection state machine: process an incoming frame and produce a state transition.
    ///
    /// Takes the current connection state and a parsed frame, then returns the updated state
    /// along with a TransitionResult indicating any side effects (errors, messages, etc.).
    let transition (conn: ConnectionState) (frame: Frame) =
        let sid = frame.Header.StreamId
        let str = findStream conn sid

        match struct (conn.PendingFields, str.State, frame.Body) with
        | ValueNone, _, GoAway _ when sid <> 0u -> protocolError conn
        | ValueNone, _, GoAway body when body.ErrorCode = ErrorCode.NO_ERROR ->
            { conn with LastStreamId = ValueSome body.Last } |> Transition.pending
        | ValueNone, _, GoAway body -> Transition.close body.ErrorCode conn
        | ValueNone, _, Ping _ when frame.Header.Length <> 8u ->
            Transition.connectionError ErrorCode.FRAME_SIZE_ERROR conn
        | ValueNone, _, Ping _ when sid <> 0u -> protocolError conn
        | ValueNone, _, Ping _ when FrameHeader.hasFlag PingFlags.ACK frame.Header -> Transition.pending conn
        | ValueNone, _, Ping body -> Transition.ping body.Data conn
        | ValueNone, _, Settings _ when sid <> 0u -> protocolError conn
        | ValueNone, _, Settings body when Frame.hasFlag SettingsFlags.ACK frame -> acknowledgeSettings body conn
        | ValueNone, _, Settings body -> updateSettings body.Settings conn
        | ValueNone, _, PushPromise _ -> protocolError conn
        | ValueNone, _, WindowUpdate body when sid = 0u -> updateConnectionWindow body.Increment conn
        // All connection-scoped frames has been processed
        | _, _, _ when sid % 2u = 0u -> protocolError conn
        | ValueNone, _, Priority _ -> Transition.pending conn
        | ValueNone, _, Continuation _ -> protocolError conn
        | ValueNone, ResettableState, Reset _ -> closeStream str conn
        | ValueNone, Closed, WindowUpdate _ -> Transition.pending conn
        | ValueNone, Closed, _ when conn.ResetStreams.Contains sid -> Transition.pending conn
        | ValueNone, Closed, _ -> Transition.connectionError ErrorCode.STREAM_CLOSED conn
        | ValueNone, (Idle | Open), Headers _ when isRefused sid conn -> Transition.streamRefused sid conn //TODO: update HPACK table
        | ValueNone, Idle, Headers body -> openStream frame.Header body conn
        | ValueNone, (Open | HalfClosedLocal), Headers body -> // trailers
            let struct (str', conn') = closeRemoteStream frame.Header str conn
            decodeFieldsFrame Trailer frame.Header body.FieldFragment str' conn'
        | ValueNone, (Open | HalfClosedLocal), Data _ when isRefused sid conn -> Transition.pending conn
        | ValueNone, (Open | HalfClosedLocal), Data body ->
            let struct (str', conn') = closeRemoteStream frame.Header str conn
            Transition.content str' body.Data conn'
        | ValueNone, (Open | HalfClosedRemote | HalfClosedLocal), WindowUpdate body ->
            updateStreamWindow str body.Increment conn
        | ValueNone, HalfClosedRemote, _ -> resetStream ErrorCode.STREAM_CLOSED str conn
        | ValueNone, HalfClosedLocal, _ -> resetStream ErrorCode.STREAM_CLOSED str conn
        | ValueSome ph, (Open | HalfClosedRemote | HalfClosedLocal | Closed), Continuation body when ph.StreamId = sid ->
            decodeFieldsFrame ph.Kind frame.Header body.FieldFragment str conn
        | _ -> protocolError conn

    let Empty =
        { NextStreamId = 1u
          LastStreamId = ValueNone
          ActiveStreams = Map.empty
          ResetStreams = Set.empty
          PendingFields = ValueNone
          HPackTable = Table.empty
          Window = FlowControlWindow.Default
          InitialWindowSize = FlowControl.DefaultWindowSize }
