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
    static member HalfClosed sid =
        { Id = sid; State = StreamState.HalfClosed; Window = FlowControlWindow.Empty }

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
            NextStreamId: StreamId
            /// Identifier of the last stream to be processed before shutting down the connection.
            LastStreamId: StreamId voption
            /// Active streams.
            ActiveStreams: Map<StreamId, H2Stream>
            /// Identifiers of the recently reset streams.
            /// Data frame sent on those streams will be ignored without error.
            ResetStreams: StreamId Set
            /// Header decoding state.
            HPackTable: DynamicTable
            /// Incomplete fields in transit.
            PendingFields: PendingFields voption
            /// Current flow control window.
            Window: FlowControlWindow
            /// Initial flow control window size. Set via <see cref="SettingType.INITIAL_WINDOW_SIZE"/>.
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

    let withPendingHeader streamId buffer = withPendingFields Header streamId buffer

    let withPendingTrailer streamId buffer = withPendingFields Trailer streamId buffer


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

    /// Find stream position based on stream ID or create a new idle stream. Position of a new stream equals -1.
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

    let inline private closeCompleteStream (fh: FrameHeader) (h2s: H2Stream) (conn: ConnectionState) =
        if FrameHeader.hasFlag END_STREAM fh then
            let h2s' = { h2s with State = HalfClosed }
            let conn' = { conn with ActiveStreams = Map.add h2s.Id h2s' conn.ActiveStreams }
            struct (h2s', conn')
        else
            h2s, conn

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
        | HalfClosed -> true
        | _ -> false

    let inline private acceptSettings (frame: Frame) setting (conn: ConnectionState) =
        if frame.Header.StreamId <> 0u then
            Transition.connectionError ErrorCode.PROTOCOL_ERROR conn
        elif Frame.hasFlag SettingsFlags.ACK frame then
            if List.isEmpty setting then
                Transition.settingsAck conn
            else
                Transition.connectionError ErrorCode.FRAME_SIZE_ERROR conn
        else
            match List.tryFindV (fun s -> s.Type = SettingType.INITIAL_WINDOW_SIZE) setting with
            | ValueNone -> Transition.clientSettings setting conn
            | ValueSome ws when ws.Value > FlowControl.MaxWindowSize ->
                Transition.connectionError ErrorCode.FLOW_CONTROL_ERROR conn
            | ValueSome windowSize ->
                let delta = int32 windowSize.Value - conn.InitialWindowSize
                conn.ActiveStreams |> Map.iter (fun _ h2s -> h2s.Window.UpdateSize delta)

                { conn with InitialWindowSize = int windowSize.Value }
                |> Transition.clientSettings setting

    let inline private acceptConnectionWindowUpdate increment conn =
        if increment = 0u then
            Transition.connectionError ErrorCode.PROTOCOL_ERROR conn
        else if not (conn.Window.TryIncrement increment) then
            Transition.connectionError ErrorCode.FLOW_CONTROL_ERROR conn
        else
            Transition.pending conn

    let inline private acceptStreamWindowUpdate (h2s: H2Stream) increment conn =
        if isRefused h2s.Id conn then
            Transition.pending conn
        elif increment = 0u then
            resetStream ErrorCode.PROTOCOL_ERROR h2s conn
        else if not (h2s.Window.TryIncrement increment) then
            resetStream ErrorCode.FLOW_CONTROL_ERROR h2s conn
        else
            Transition.pending conn

    let inline private acceptPing fh body conn =
        if fh.Length <> 8u then
            Transition.connectionError ErrorCode.FRAME_SIZE_ERROR conn
        elif fh.StreamId <> 0u then
            Transition.connectionError ErrorCode.PROTOCOL_ERROR conn
        elif FrameHeader.hasFlag PingFlags.ACK fh then
            Transition.pending conn
        else
            Transition.ping body conn

    let inline private acceptGoAway (fh: FrameHeader) (body: GoAwayBody) conn =
        if fh.StreamId <> 0u then
            Transition.connectionError ErrorCode.PROTOCOL_ERROR conn
        elif body.ErrorCode = ErrorCode.NO_ERROR then
            { conn with LastStreamId = ValueSome body.Last } |> Transition.pending
        else
            Transition.close body.ErrorCode conn

    let inline private acceptOnClosedStream (frame: Frame) (conn: ConnectionState) =
        if frame.Body.IsWindowUpdate then
            Transition.pending conn
        elif conn.ResetStreams.Contains frame.Header.StreamId then
            Transition.pending conn
        else
            Transition.connectionError ErrorCode.STREAM_CLOSED conn

    let inline private closeStream (h2s: H2Stream) (conn: ConnectionState) =
        if h2s.State = StreamState.Closed then
            Transition.pending conn
        else
            { conn with
                ActiveStreams = conn.ActiveStreams.Remove h2s.Id
                ResetStreams = conn.ResetStreams.Add h2s.Id }
            |> Transition.streamClosed h2s.Id

    let inline private openStream (fh: FrameHeader) (hb: HeadersBody) (conn: ConnectionState) =
        let state =
            if FrameHeader.hasFlag END_STREAM fh then
                HalfClosed
            else
                Open

        let h2s = { Id = fh.StreamId; State = state; Window = FlowControlWindow.Default }
        conn |> addStream h2s |> decodeFieldsFrame Header fh hb.FieldFragment h2s

    let transition (conn: ConnectionState) (frame: Frame) =
        let sid = frame.Header.StreamId
        let str = findStream conn sid

        match struct (conn.PendingFields, str.State, frame.Body) with
        | ValueNone, _, Priority _ -> Transition.pending conn
        | ValueNone, _, GoAway body -> acceptGoAway frame.Header body conn
        | ValueNone, _, Ping body -> acceptPing frame.Header body.Data conn
        | ValueNone, _, Settings body -> acceptSettings frame body.Settings conn
        | ValueNone, _, WindowUpdate body when sid = 0u -> acceptConnectionWindowUpdate body.Increment conn
        | ValueNone, ResettableState, Reset _ -> closeStream str conn
        | ValueNone, Closed, _ -> acceptOnClosedStream frame conn
        | ValueNone, (Idle | Open), Headers _ when isRefused sid conn -> Transition.streamRefused sid conn //TODO: update HPACK table
        | ValueNone, Idle, Headers body -> openStream frame.Header body conn
        | ValueNone, Open, Headers body -> // trailers
            let struct (str', conn') = closeCompleteStream frame.Header str conn
            decodeFieldsFrame Trailer frame.Header body.FieldFragment str' conn'
        | ValueNone, Open, Data _ when isRefused sid conn -> Transition.pending conn
        | ValueNone, Open, Data body ->
            let struct (str', conn') = closeCompleteStream frame.Header str conn
            Transition.content str' body.Data conn'
        | ValueNone, (Open | HalfClosed), WindowUpdate body -> acceptStreamWindowUpdate str body.Increment conn
        | ValueSome ph, ResettableState, Reset _ when ph.StreamId <> sid -> closeStream str conn
        | ValueSome ph, (Open | HalfClosed), Continuation body when ph.StreamId = sid ->
            decodeFieldsFrame ph.Kind frame.Header body.FieldFragment str conn
        | _ -> Transition.connectionError ErrorCode.PROTOCOL_ERROR conn

    let Empty =
        { NextStreamId = 1u
          LastStreamId = ValueNone
          ActiveStreams = Map.empty
          ResetStreams = Set.empty
          PendingFields = ValueNone
          HPackTable = Table.empty
          Window = FlowControlWindow.Default
          InitialWindowSize = FlowControl.DefaultWindowSize }
