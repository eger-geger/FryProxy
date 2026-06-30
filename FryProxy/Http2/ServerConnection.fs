namespace FryProxy.Http2

open System
open System.Buffers
open FryProxy.Extension
open FryProxy.Http2.Frames
open FryProxy.Http2.Frames.FrameFlags
open FryProxy.Http2.Hpack
open FryProxy.IO

// Incomplete header data transmitted on a given stream.
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

[<Struct; CustomEquality; NoComparison>]
type ServerConnection =
    internal
        { NextStreamId: StreamId
          Streams: HttpStream List
          HPackTable: DynamicTable
          PendingHeader: PendingHeader voption }

    interface IEquatable<ServerConnection> with
        member this.Equals(other: ServerConnection) =
            this.NextStreamId = other.NextStreamId
            && this.Streams = other.Streams
            && this.HPackTable = other.HPackTable
            && this.PendingHeader = other.PendingHeader


[<Struct>]
type MessagePart =
    | Nothing
    | Content of Bytes: IByteBuffer
    | Fields of Fields: FieldPack List
    | ResetStream of ErrorCode

type TransitionResult = Result<struct (MessagePart * ServerConnection), ErrorCode>

module Transition =
    let inline error code : TransitionResult = Error(code)

    let inline reset code cnx : TransitionResult = Ok(ResetStream code, cnx)

    let inline content data cnx : TransitionResult = Ok(Content data, cnx)

    let inline fields fields cnx : TransitionResult = Ok(Fields fields, cnx)

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
            { Id = id; State = StreamState.Idle }
        else
            List.tryFindV (fun s -> s.Id = id) conn.Streams
            |> ValueOption.defaultValue { Id = id; State = StreamState.Closed }

    let inline private addStream (conn: ServerConnection) stream =
        { conn with Streams = stream :: conn.Streams; NextStreamId = stream.Id + 2u }

    let inline private updateStream (conn: ServerConnection) stream =
        let inline streamById { Id = id } =
            if id = stream.Id then ValueSome stream else ValueNone

        { conn with Streams = List.replaceFirst streamById conn.Streams }

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

    let inline private closeCompleteStream header conn =
        if FrameHeader.hasFlag END_STREAM header then
            { Id = header.StreamId; State = StreamState.HalfClosed } |> updateStream conn
        else
            conn

    let inline private resetStream stream errorCode conn =
        if stream.State = StreamState.Closed then
            Transition.pending conn
        else
            { conn with
                Streams = List.removeFirst (fun s -> s.Id = stream.Id) conn.Streams }
            |> Transition.reset errorCode

    let (|ResettableState|_|) state =
        match state with
        | Open
        | Closed
        | Reserved
        | HalfClosed -> true
        | _ -> false


    let transition (conn: ServerConnection) (frame: Frame) =
        let stream = findStream conn frame.Header.StreamId

        match struct (conn.PendingHeader, stream.State, frame.Body) with
        | ValueNone, _, Priority _ -> Transition.pending conn
        | ValueNone, StreamState.Idle, Headers body ->
            { stream with State = StreamState.Open }
            |> addStream conn
            |> closeCompleteStream frame.Header
            |> decodeHeaderFrame frame.Header body.FieldFragment
        | ValueNone, StreamState.Open, Headers body ->
            conn
            |> closeCompleteStream frame.Header
            |> decodeHeaderFrame frame.Header body.FieldFragment
        | ValueNone, StreamState.Open, Data body ->
            conn |> closeCompleteStream frame.Header |> Transition.content body.Data
        | ValueNone, ResettableState, Reset body -> resetStream stream body.ErrorCode conn
        | ValueSome ph, ResettableState, Reset body when ph.StreamId <> stream.Id ->
            resetStream stream body.ErrorCode conn
        | ValueSome ph, StreamState.Open, Continuation body when ph.StreamId = stream.Id ->
            decodeHeaderFrame frame.Header body.FieldFragment conn
        | _ -> Error ErrorCode.PROTOCOL_ERROR

    let Empty =
        { NextStreamId = 1u
          Streams = List.Empty
          PendingHeader = ValueNone
          HPackTable = Table.empty }
