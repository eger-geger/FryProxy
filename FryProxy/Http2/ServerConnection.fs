namespace FryProxy.Http2

open System
open System.Buffers
open FryProxy.Extension
open FryProxy.Http2.Frames
open FryProxy.Http2.Frames.FrameFlags
open FryProxy.Http2.Hpack

[<Struct; CustomEquality; NoComparison>]
type ServerConnection =
    internal
        { Streams: HttpStream List
          HpackTable: DynamicTable
          FieldBuffer: byte SizedBuffer }

    interface IEquatable<ServerConnection> with
        member this.Equals(other: ServerConnection) =
            let thisFieldBuf = (this.FieldBuffer :> IMemoryOwner<_>).Memory
            let otherFieldBuf = (other.FieldBuffer :> IMemoryOwner<_>).Memory

            this.Streams = other.Streams
            && this.HpackTable = other.HpackTable
            && thisFieldBuf.Length = otherFieldBuf.Length
            && thisFieldBuf.Span.SequenceEqual(otherFieldBuf.Span)


[<Struct>]
type MessagePart =
    | Nothing
    | Fields of Fields: FieldPack List

type TransitionResult = Result<struct (MessagePart * ServerConnection), ErrorCode>

module Transition =
    let error code : TransitionResult = Error(code)

    let pending conn : TransitionResult = Ok(Nothing, conn)

    let headers fields conn : TransitionResult = Ok(Fields fields, conn)

module ServerConnection =

    /// Find stream position based on stream ID or create a new idle stream. Position of a new stream equals -1.
    let inline private findStream (conn: ServerConnection) id =
        conn.Streams
        |> List.indexed
        |> List.tryFind (fun (_, s) -> s.Id = id)
        |> Option.defaultValue (-1, { Id = id; State = StreamState.Idle })

    let inline private insertStream (conn: ServerConnection) pos stream =
        let streams' =
            match pos with
            | -1 -> stream :: conn.Streams
            | n -> conn.Streams |> List.updateAt n stream

        { conn with Streams = streams' }

    let decodeFieldFragment (flags: HeadersFlags) (body: HeadersBody) (conn: ServerConnection) : TransitionResult =
        if flags.HasFlag(HeadersFlags.END_HEADERS) then
            match Table.decodeFields conn.HpackTable body.FieldFragment.Span with
            | Ok(fields, table) ->
                let conn' = { conn with FieldBuffer = SizedBuffer.Empty; HpackTable = table }

                Ok(Fields fields, conn')
            | Error _ -> Error ErrorCode.COMPRESSION_ERROR
        else
            let buf = MemoryPool.Shared.Append conn.FieldBuffer body.FieldFragment
            Transition.pending { conn with FieldBuffer = buf }

    let private transitionIdle (frame: Frame) conn =
        match frame.Body with
        | Headers headers -> decodeFieldFragment <| FrameHeader.frameFlags frame.Header <|| (headers, conn)
        | Priority _ -> Transition.pending conn
        | _ -> Error ErrorCode.PROTOCOL_ERROR

    let inline private closeCompleteStream flags stream =
        if flags &&& END_STREAM = END_STREAM then
            { stream with State = StreamState.Closed }
        else
            stream

    let transition (conn: ServerConnection) (frame: Frame) =
        let pos, stream = findStream conn frame.Header.StreamId

        match stream.State with
        | Idle ->
            { stream with State = StreamState.Open }
            |> closeCompleteStream frame.Header.Flags
            |> insertStream conn pos
            |> transitionIdle frame
        | Open -> failwith "todo"
        | Closed -> failwith "todo"
        | Reserved -> failwith "todo"
        | HalfClosed -> failwith "todo"

    let Empty =
        { Streams = List.Empty
          HpackTable = Table.empty
          FieldBuffer = SizedBuffer.Empty }
