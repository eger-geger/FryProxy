namespace FryProxy.Http2

open System
open System.Buffers
open System.Diagnostics
open FryProxy.Extension
open FryProxy.Http2.Frames
open FryProxy.Http2.FrameFlags
open FryProxy.Http2.Hpack

/// Incomplete header data transmitted on a given stream.
[<Struct; CustomEquality; NoComparison>]
type PendingHeader =
    internal
        { StreamId: StreamId
          Buffer: byte SizedBuffer }

    interface IEquatable<PendingHeader> with
        member this.Equals(other: PendingHeader) =
            this.StreamId = other.StreamId
            && this.Buffer.Size = other.Buffer.Size
            && this.Buffer.Span.SequenceEqual(other.Buffer.Span)


[<Struct; CustomEquality; CustomComparison>]
[<StructuredFormatDisplay("{DisplayText}"); DebuggerDisplay("{DisplayText,nq}")>]
type H2Stream =
    internal
        { Id: StreamId
          State: StreamState
          Window: FlowControlWindow }

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

    override this.Equals(obj: obj) =
        match obj with
        | :? H2Stream as other -> this.Id = other.Id && this.State = other.State
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
            /// Incomplete header data being transmitted.
            PendingHeader: PendingHeader voption
            /// Current flow control window.
            Window: FlowControlWindow
        }

    interface IEquatable<ConnectionState> with
        member this.Equals(other: ConnectionState) =
            this.NextStreamId = other.NextStreamId
            && this.ActiveStreams = other.ActiveStreams
            && this.ResetStreams = other.ResetStreams
            && this.HPackTable = other.HPackTable
            && this.PendingHeader = other.PendingHeader


[<Struct>]
type TransitionResult =
    | None
    | ConnectionError of Code: ErrorCode
    | StreamError of StreamId: StreamId * Code: ErrorCode
    | PingRequest of Bytes: byte ReadOnlyMemory
    | MessageBody of StreamId: StreamId * Data: Octets * EOS: bool
    | MessageFields of Stream: H2Stream * Fields: FieldPack List * EOS: bool
    | StreamReset of StreamId: StreamId * Code: ErrorCode
    | ConnectionClose of Code: ErrorCode
    | ConnectionWindowUpdate of Increment: uint32
    | StreamWindowUpdate of StreamId: StreamId * Increment: uint32
    | SettingsAck
    | ClientSettings of Settings: Setting List

type ConnectionStateTransition = (struct (ConnectionState * TransitionResult))

module Transition =
    let inline connectionError code conn : ConnectionStateTransition = conn, ConnectionError code

    let inline streamError sid code conn : ConnectionStateTransition = conn, StreamError(sid, code)

    let inline ping data cnx : ConnectionStateTransition = cnx, PingRequest data

    let inline reset sid code cnx : ConnectionStateTransition = cnx, StreamReset(sid, code)

    let inline close code cnx : ConnectionStateTransition = cnx, ConnectionClose code

    let inline connectionWindowUpdate inc cnx : ConnectionStateTransition = cnx, ConnectionWindowUpdate inc

    let inline streamWindowUpdate id inc cnx : ConnectionStateTransition = cnx, StreamWindowUpdate(id, inc)

    let inline clientSettings settings cnx : ConnectionStateTransition = cnx, ClientSettings settings

    let inline settingsAck cnx : ConnectionStateTransition = cnx, SettingsAck

    let inline content sid data eos cnx : ConnectionStateTransition = cnx, MessageBody(sid, data, eos)

    let inline fields h2s fields eos cnx : ConnectionStateTransition = cnx, MessageFields(h2s, fields, eos)

    let inline pending conn : ConnectionStateTransition = conn, None

    let pendingFields streamId buffer conn : ConnectionStateTransition =
        { conn with PendingHeader = ValueSome { StreamId = streamId; Buffer = buffer } }
        |> pending

    let decodedFields h2s fieldList eos table conn : ConnectionStateTransition =
        { conn with PendingHeader = ValueNone; HPackTable = table }
        |> fields h2s fieldList eos

module ServerConnection =

    /// Find stream position based on stream ID or create a new idle stream. Position of a new stream equals -1.
    let inline private findStream (conn: ConnectionState) id =
        if id >= conn.NextStreamId then
            { Id = id; State = StreamState.Idle; Window = FlowControlWindow.Default }
        else
            Map.tryFind id conn.ActiveStreams
            |> Option.defaultValue { Id = id; State = StreamState.Closed; Window = FlowControlWindow.Empty }

    let inline private addStream h2s (conn: ConnectionState) =
        { conn with
            NextStreamId = h2s.Id + 2u
            ActiveStreams = conn.ActiveStreams |> Map.add h2s.Id h2s }

    let inline private updateStream sid state (conn: ConnectionState) =
        let update s = { s with State = state }

        { conn with
            ActiveStreams = conn.ActiveStreams |> Map.change sid (Option.map update) }

    let inline private isRefused sid conn =
        conn.LastStreamId |> ValueOption.map ((>) sid) |> ValueOption.defaultValue false

    let decodeHeaderFrame (h2s: H2Stream) header body (conn: ConnectionState) : ConnectionStateTransition =
        let fieldBuff =
            conn.PendingHeader
            |> ValueOption.map _.Buffer
            |> ValueOption.defaultValue SizedBuffer.Empty
            |> MemoryPool.Shared.Append
            <| body

        if FrameHeader.hasFlag END_HEADERS header then
            match Table.decodeFields conn.HPackTable fieldBuff.Span with
            | Ok(fields, table) ->
                Transition.decodedFields h2s fields (FrameHeader.hasFlag END_STREAM header) table conn
            | Error _ -> Transition.connectionError ErrorCode.COMPRESSION_ERROR conn
        else
            Transition.pendingFields header.StreamId fieldBuff conn

    let private acceptContinuation (frame: Frame) conn h2s =
        match frame.Body with
        | Continuation body -> decodeHeaderFrame h2s frame.Header body.FieldFragment conn
        | _ -> Transition.connectionError ErrorCode.PROTOCOL_ERROR conn

    let inline private closeCompleteStream header =
        if FrameHeader.hasFlag END_STREAM header then
            updateStream header.StreamId StreamState.HalfClosed
        else
            id

    let inline private resetStream streamId streamState errorCode conn =
        if streamState = StreamState.Closed then
            Transition.pending conn
        else
            { conn with
                ResetStreams = conn.ResetStreams.Add streamId
                ActiveStreams = conn.ActiveStreams.Remove streamId }
            |> Transition.reset streamId errorCode

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
            Transition.clientSettings setting conn

    let inline private acceptConnectionWindowUpdate increment conn =
        if increment = 0u then
            Transition.connectionError ErrorCode.FLOW_CONTROL_ERROR conn
        else
            Transition.connectionWindowUpdate increment conn

    let inline private acceptStreamWindowUpdate sid increment conn =
        if isRefused sid conn then
            Transition.streamError sid ErrorCode.REFUSED_STREAM conn
        elif increment = 0u then
            Transition.streamError sid ErrorCode.FLOW_CONTROL_ERROR conn
        else
            Transition.streamWindowUpdate sid increment conn

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
            Transition.streamError frame.Header.StreamId ErrorCode.STREAM_CLOSED conn

    let inline private acceptOnIdleStream (frame: Frame) (conn: ConnectionState) =
        match frame.Body with
        | WindowUpdate _ -> Transition.connectionError ErrorCode.PROTOCOL_ERROR conn
        | Headers _ when conn.LastStreamId.IsSome ->
            Transition.streamError frame.Header.StreamId ErrorCode.REFUSED_STREAM conn
        | Headers body ->
            let state =
                if Frame.hasFlag HeadersFlags.END_STREAM frame then
                    StreamState.HalfClosed
                else
                    StreamState.Open

            let h2s =
                { Id = frame.Header.StreamId
                  State = state
                  Window = FlowControlWindow.Default }

            conn |> addStream h2s |> decodeHeaderFrame h2s frame.Header body.FieldFragment
        | _ -> Transition.connectionError ErrorCode.PROTOCOL_ERROR conn

    let inline private acceptHeaders h2s (fh: FrameHeader) (body: HeadersBody) conn =
        if isRefused fh.StreamId conn then
            Transition.streamError fh.StreamId ErrorCode.REFUSED_STREAM conn
        else
            conn |> closeCompleteStream fh |> decodeHeaderFrame h2s fh body.FieldFragment

    let inline private acceptDataFrame (fh: FrameHeader) (body: DataBody) conn =
        if isRefused fh.StreamId conn then
            Transition.streamError fh.StreamId ErrorCode.REFUSED_STREAM conn
        else
            conn
            |> closeCompleteStream fh
            |> Transition.content fh.StreamId body.Data (FrameHeader.hasFlag END_STREAM fh)

    let transition (conn: ConnectionState) (frame: Frame) =
        let sid = frame.Header.StreamId
        let str = findStream conn sid

        match struct (conn.PendingHeader, str.State, frame.Body) with
        | ValueNone, _, Priority _ -> Transition.pending conn
        | ValueNone, _, GoAway body -> acceptGoAway frame.Header body conn
        | ValueNone, _, Ping body -> acceptPing frame.Header body.Data conn
        | ValueNone, _, Settings body -> acceptSettings frame body.Settings conn
        | ValueNone, _, WindowUpdate body when sid = 0u -> acceptConnectionWindowUpdate body.Increment conn
        | ValueNone, ResettableState, Reset body -> resetStream sid str.State body.ErrorCode conn
        | ValueNone, StreamState.Closed, _ -> acceptOnClosedStream frame conn
        | ValueNone, StreamState.Idle, _ -> acceptOnIdleStream frame conn
        | ValueNone, StreamState.Open, Headers body -> acceptHeaders str frame.Header body conn
        | ValueNone, StreamState.Open, Data body -> acceptDataFrame frame.Header body conn
        | ValueNone, (StreamState.Open | StreamState.HalfClosed), WindowUpdate body ->
            acceptStreamWindowUpdate sid body.Increment conn
        | ValueSome ph, ResettableState, Reset body when ph.StreamId <> sid ->
            resetStream sid str.State body.ErrorCode conn
        | ValueSome ph, StreamState.Open, Continuation body when ph.StreamId = sid ->
            decodeHeaderFrame str frame.Header body.FieldFragment conn
        | _ -> Transition.connectionError ErrorCode.PROTOCOL_ERROR conn

    let Empty =
        { NextStreamId = 1u
          LastStreamId = ValueNone
          ActiveStreams = Map.empty
          ResetStreams = Set.empty
          PendingHeader = ValueNone
          HPackTable = Table.empty
          Window = FlowControlWindow.Default }
