namespace FryProxy.Http2

open System
open System.Buffers
open FryProxy.Extension
open FryProxy.Http2.Frames
open FryProxy.Http2.Frames.FrameFlags
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
            ActiveStreams: Map<StreamId, StreamState>
            /// Identifiers of the recently reset streams.
            /// Data frame sent on those streams will be ignored without error.
            ResetStreams: StreamId Set
            /// Header decoding state.
            HPackTable: DynamicTable
            /// Incomplete header data being transmitted.
            PendingHeader: PendingHeader voption
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
    | MessageBody of Stream: Octets
    | MessageFields of Fields: FieldPack List
    | StreamReset of Code: ErrorCode
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

    let inline reset code cnx : ConnectionStateTransition = cnx, StreamReset code

    let inline close code cnx : ConnectionStateTransition = cnx, ConnectionClose code

    let inline connectionWindowUpdate inc cnx : ConnectionStateTransition = cnx, ConnectionWindowUpdate inc

    let inline streamWindowUpdate id inc cnx : ConnectionStateTransition = cnx, StreamWindowUpdate(id, inc)

    let inline clientSettings settings cnx : ConnectionStateTransition = cnx, ClientSettings settings

    let inline settingsAck cnx : ConnectionStateTransition = cnx, SettingsAck

    let inline content data cnx : ConnectionStateTransition = cnx, MessageBody data

    let inline fields fields cnx : ConnectionStateTransition = cnx, MessageFields fields

    let inline pending conn : ConnectionStateTransition = conn, None

    let pendingFields streamId buffer conn : ConnectionStateTransition =
        { conn with PendingHeader = ValueSome { StreamId = streamId; Buffer = buffer } }
        |> pending

    let decodedFields fieldList table conn : ConnectionStateTransition =
        { conn with PendingHeader = ValueNone; HPackTable = table } |> fields fieldList

module ServerConnection =

    /// Find stream position based on stream ID or create a new idle stream. Position of a new stream equals -1.
    let inline private findStream (conn: ConnectionState) id =
        if id >= conn.NextStreamId then
            StreamState.Idle
        else
            Map.tryFind id conn.ActiveStreams |> Option.defaultValue StreamState.Closed

    let inline private addStream id state (conn: ConnectionState) =
        { conn with
            NextStreamId = id + 2u
            ActiveStreams = Map.add id state conn.ActiveStreams }

    let inline private updateStream streamId state (conn: ConnectionState) =
        { conn with
            ActiveStreams = conn.ActiveStreams |> Map.change streamId (Option.map (fun _ -> state)) }

    let inline private isRefused sid conn =
        conn.LastStreamId |> ValueOption.map ((>) sid) |> ValueOption.defaultValue false

    let decodeHeaderFrame header body (conn: ConnectionState) : ConnectionStateTransition =
        let fieldBuff =
            conn.PendingHeader
            |> ValueOption.map _.Buffer
            |> ValueOption.defaultValue SizedBuffer.Empty
            |> MemoryPool.Shared.Append
            <| body

        if FrameHeader.hasFlag END_HEADERS header then
            match Table.decodeFields conn.HPackTable fieldBuff.Span with
            | Ok(fields, table) -> Transition.decodedFields fields table conn
            | Error _ -> Transition.connectionError ErrorCode.COMPRESSION_ERROR conn
        else
            Transition.pendingFields header.StreamId fieldBuff conn

    let private acceptContinuation (frame: Frame) conn =
        match frame.Body with
        | Continuation body -> decodeHeaderFrame frame.Header body.FieldFragment conn
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
            |> Transition.reset errorCode

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

            conn
            |> addStream frame.Header.StreamId state
            |> decodeHeaderFrame frame.Header body.FieldFragment
        | _ -> Transition.connectionError ErrorCode.PROTOCOL_ERROR conn

    let inline private acceptHeaders (fh: FrameHeader) (body: HeadersBody) conn =
        if isRefused fh.StreamId conn then
            Transition.streamError fh.StreamId ErrorCode.REFUSED_STREAM conn
        else
            conn |> closeCompleteStream fh |> decodeHeaderFrame fh body.FieldFragment

    let inline private acceptDataFrame (fh: FrameHeader) (body: DataBody) conn =
        if isRefused fh.StreamId conn then
            Transition.streamError fh.StreamId ErrorCode.REFUSED_STREAM conn
        else
            conn |> closeCompleteStream fh |> Transition.content body.Data

    let transition (conn: ConnectionState) (frame: Frame) =
        let sid = frame.Header.StreamId
        let streamState = findStream conn sid

        match struct (conn.PendingHeader, streamState, frame.Body) with
        | ValueNone, _, Priority _ -> Transition.pending conn
        | ValueNone, _, GoAway body -> acceptGoAway frame.Header body conn
        | ValueNone, _, Ping body -> acceptPing frame.Header body.Data conn
        | ValueNone, _, Settings body -> acceptSettings frame body.Settings conn
        | ValueNone, _, WindowUpdate body when sid = 0u -> acceptConnectionWindowUpdate body.Increment conn
        | ValueNone, ResettableState, Reset body -> resetStream sid streamState body.ErrorCode conn
        | ValueNone, StreamState.Closed, _ -> acceptOnClosedStream frame conn
        | ValueNone, StreamState.Idle, _ -> acceptOnIdleStream frame conn
        | ValueNone, StreamState.Open, Headers body -> acceptHeaders frame.Header body conn
        | ValueNone, StreamState.Open, Data body -> acceptDataFrame frame.Header body conn
        | ValueNone, (StreamState.Open | StreamState.HalfClosed), WindowUpdate body ->
            acceptStreamWindowUpdate sid body.Increment conn
        | ValueSome ph, ResettableState, Reset body when ph.StreamId <> sid ->
            resetStream sid streamState body.ErrorCode conn
        | ValueSome ph, StreamState.Open, Continuation body when ph.StreamId = sid ->
            decodeHeaderFrame frame.Header body.FieldFragment conn
        | _ -> Transition.connectionError ErrorCode.PROTOCOL_ERROR conn

    let Empty =
        { NextStreamId = 1u
          LastStreamId = ValueNone
          ActiveStreams = Map.empty
          ResetStreams = Set.empty
          PendingHeader = ValueNone
          HPackTable = Table.empty }
