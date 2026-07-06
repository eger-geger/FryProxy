namespace FryProxy.Http2

open System
open System.Buffers
open FryProxy.Extension
open FryProxy.Http2.Frames
open FryProxy.Http2.Frames.FrameFlags
open FryProxy.Http2.Hpack
open FryProxy.IO

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
type ServerConnection =
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

    interface IEquatable<ServerConnection> with
        member this.Equals(other: ServerConnection) =
            this.NextStreamId = other.NextStreamId
            && this.ActiveStreams = other.ActiveStreams
            && this.ResetStreams = other.ResetStreams
            && this.HPackTable = other.HPackTable
            && this.PendingHeader = other.PendingHeader


[<Struct>]
type MessagePart =
    | Nothing
    | PingRequest of Bytes: IByteBuffer
    | MessageBody of Bytes: IByteBuffer
    | MessageFields of Fields: FieldPack List
    | StreamReset of ErrorCode
    | ConnectionClose of ErrorCode
    | ConnectionWindowUpdate of Increment: uint32
    | StreamWindowUpdate of StreamId: StreamId * Increment: uint32
    | SettingsAck
    | ClientSettings of Settings: Setting List

type TransitionResult = Result<struct (MessagePart * ServerConnection), ErrorCode>

module Transition =
    let inline error code : TransitionResult = Error(code)

    let inline ping data cnx : TransitionResult = Ok(PingRequest data, cnx)

    let inline reset code cnx : TransitionResult = Ok(StreamReset code, cnx)

    let inline close code cnx : TransitionResult = Ok(ConnectionClose code, cnx)

    let inline connectionWindowUpdate inc cnx : TransitionResult = Ok(ConnectionWindowUpdate inc, cnx)

    let inline streamWindowUpdate id inc cnx : TransitionResult = Ok(StreamWindowUpdate(id, inc), cnx)

    let inline clientSettings settings cnx : TransitionResult = Ok(ClientSettings settings, cnx)

    let inline settingsAck cnx : TransitionResult = Ok(SettingsAck, cnx)

    let inline content data cnx : TransitionResult = Ok(MessageBody data, cnx)

    let inline fields fields cnx : TransitionResult = Ok(MessageFields fields, cnx)

    let inline pending conn : TransitionResult = Ok(Nothing, conn)

    let pendingFields streamId buffer conn : TransitionResult =
        { conn with PendingHeader = ValueSome { StreamId = streamId; Buffer = buffer } }
        |> pending

    let decodedFields fieldList table conn : TransitionResult =
        { conn with PendingHeader = ValueNone; HPackTable = table } |> fields fieldList

module ServerConnection =

    /// Find stream position based on stream ID or create a new idle stream. Position of a new stream equals -1.
    let inline private findStream (conn: ServerConnection) id =
        if id >= conn.NextStreamId then
            StreamState.Idle
        else
            Map.tryFind id conn.ActiveStreams |> Option.defaultValue StreamState.Closed

    let inline private addStream id state (conn: ServerConnection) =
        { conn with
            NextStreamId = id + 2u
            ActiveStreams = Map.add id state conn.ActiveStreams }

    let inline private updateStream streamId state (conn: ServerConnection) =
        { conn with
            ActiveStreams = conn.ActiveStreams |> Map.change streamId (Option.map (fun _ -> state)) }

    let inline private isRefused sid conn =
        conn.LastStreamId |> ValueOption.map ((>) sid) |> ValueOption.defaultValue false

    let decodeHeaderFrame header body (conn: ServerConnection) : TransitionResult =
        let fieldBuff =
            conn.PendingHeader
            |> ValueOption.map _.Buffer
            |> ValueOption.defaultValue SizedBuffer.Empty
            |> MemoryPool.Shared.Append
            <| body

        if FrameHeader.hasFlag END_HEADERS header then
            match Table.decodeFields conn.HPackTable fieldBuff.Span with
            | Ok(fields, table) -> Transition.decodedFields fields table conn
            | Error _ -> Error ErrorCode.COMPRESSION_ERROR
        else
            Transition.pendingFields header.StreamId fieldBuff conn

    let private acceptContinuation (frame: Frame) conn =
        match frame.Body with
        | Continuation body -> decodeHeaderFrame frame.Header body.FieldFragment conn
        | _ -> Error ErrorCode.PROTOCOL_ERROR

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

    let inline private acceptSettings (frame: Frame) setting (conn: ServerConnection) =
        if frame.Header.StreamId <> 0u then
            Error ErrorCode.PROTOCOL_ERROR
        elif Frame.hasFlag SettingsFlags.ACK frame then
            if List.isEmpty setting then
                Transition.settingsAck conn
            else
                Error ErrorCode.FRAME_SIZE_ERROR
        else
            Transition.clientSettings setting conn

    let inline private acceptConnectionWindowUpdate increment conn =
        if increment = 0u then
            Error ErrorCode.FLOW_CONTROL_ERROR
        else
            Transition.connectionWindowUpdate increment conn

    let inline private acceptStreamWindowUpdate sid increment conn =
        if isRefused sid conn then Error ErrorCode.REFUSED_STREAM
        elif increment = 0u then Error ErrorCode.FLOW_CONTROL_ERROR
        else Transition.streamWindowUpdate sid increment conn

    let inline private acceptPing fh body conn =
        if fh.Length <> 8u then
            Error ErrorCode.FRAME_SIZE_ERROR
        elif fh.StreamId <> 0u then
            Error ErrorCode.PROTOCOL_ERROR
        elif FrameHeader.hasFlag PingFlags.ACK fh then
            Transition.pending conn
        else
            Transition.ping body conn

    let inline private acceptGoAway (fh: FrameHeader) (body: GoAwayBody) conn =
        if fh.StreamId <> 0u then
            Error ErrorCode.PROTOCOL_ERROR
        elif body.ErrorCode = ErrorCode.NO_ERROR then
            { conn with LastStreamId = ValueSome body.Last } |> Transition.pending
        else
            Transition.close body.ErrorCode conn

    let inline private acceptOnClosedStream (frame: Frame) (conn: ServerConnection) =
        if frame.Body.IsWindowUpdate then
            Transition.pending conn
        elif conn.ResetStreams.Contains frame.Header.StreamId then
            Transition.pending conn
        else
            Error ErrorCode.STREAM_CLOSED

    let inline private acceptOnIdleStream (frame: Frame) (conn: ServerConnection) =
        match frame.Body with
        | WindowUpdate _ -> Error ErrorCode.PROTOCOL_ERROR
        | Headers _ when conn.LastStreamId.IsSome -> Error ErrorCode.REFUSED_STREAM
        | Headers body ->
            let state =
                if Frame.hasFlag HeadersFlags.END_STREAM frame then
                    StreamState.HalfClosed
                else
                    StreamState.Open

            conn
            |> addStream frame.Header.StreamId state
            |> decodeHeaderFrame frame.Header body.FieldFragment
        | _ -> Error ErrorCode.PROTOCOL_ERROR

    let inline private acceptHeaders (fh: FrameHeader) (body: HeadersBody) conn =
        if isRefused fh.StreamId conn then
            Error ErrorCode.REFUSED_STREAM
        else
            conn |> closeCompleteStream fh |> decodeHeaderFrame fh body.FieldFragment

    let inline private acceptDataFrame (fh: FrameHeader) (body: DataBody) conn =
        if isRefused fh.StreamId conn then
            Error ErrorCode.REFUSED_STREAM
        else
            conn |> closeCompleteStream fh |> Transition.content body.Data

    let transition (conn: ServerConnection) (frame: Frame) =
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
        | _ -> Error ErrorCode.PROTOCOL_ERROR

    let Empty =
        { NextStreamId = 1u
          LastStreamId = ValueNone
          ActiveStreams = Map.empty
          ResetStreams = Set.empty
          PendingHeader = ValueNone
          HPackTable = Table.empty }
