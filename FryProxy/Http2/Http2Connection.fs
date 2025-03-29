namespace FryProxy.Http2

open System
open System.Buffers
open FryProxy.Http2.Frames
open FryProxy.Http2.Hpack

[<Struct>]
type Http2Connection =
    internal
        { Streams: HttpStream List
          HpackTable: DynamicTable
          FieldBlock: byte IMemoryOwner }

[<Struct>]
type MessagePart =
    | Nothing
    | Fields of Fields: FieldPack List

type TransitionResult = Result<struct (MessagePart * Http2Connection), ErrorCode>

module Transition =
    let error code : TransitionResult = Error(code)

    let pending conn : TransitionResult = Ok(Nothing, conn)

module Http2Connection =

    let private emptyFieldBlock =
        { new IMemoryOwner<byte> with
            member this.Memory = Memory.Empty
            member this.Dispose() : unit = () }

    /// Find stream position based on stream ID or create a new idle stream. Position of a new stream equals -1.
    let inline private findStream (conn: Http2Connection) id =
        conn.Streams
        |> List.indexed
        |> List.tryFind(fun (_, s) -> s.Id = id)
        |> Option.defaultValue(-1, { Id = id; State = StreamState.Idle })

    let inline private updateStream (conn: Http2Connection) pos stream =
        let streams' =
            match pos with
            | -1 -> stream :: conn.Streams
            | n -> conn.Streams |> List.updateAt n stream

        { conn with Streams = streams' }

    let decodeFieldBlock (flags: HeadersFlags) (body: HeadersBody) (conn: Http2Connection) : TransitionResult =
        if flags.HasFlag(HeadersFlags.END_HEADERS) then
            match Table.decodeFields conn.HpackTable body.FieldBlock.Span with
            | Ok(fields, table) ->
                let conn' = { conn with FieldBlock = emptyFieldBlock; HpackTable = table }
                Ok struct (Fields fields, conn')
            | Error _ -> Error ErrorCode.COMPRESSION_ERROR
        else
            use oldBuf = conn.FieldBlock
            let newBuf = MemoryPool.Shared.Rent(oldBuf.Memory.Length + body.FieldBlock.Length)

            do oldBuf.Memory.CopyTo(newBuf.Memory.Slice(0, oldBuf.Memory.Length))
            do body.FieldBlock.CopyTo(newBuf.Memory.Slice(oldBuf.Memory.Length))

            Transition.pending { conn with FieldBlock = newBuf }

    let private transitionIdle (frame: Frame) conn =
        match frame.Body with
        | Headers headers ->
            let flags = FrameHeader.frameFlags frame.Header
            decodeFieldBlock flags headers conn
        | Priority _ -> Transition.pending conn
        | _ -> Error ErrorCode.PROTOCOL_ERROR

    let transition (conn: Http2Connection) (frame: Frame) =
        let pos, stream = findStream conn frame.Header.StreamId

        match stream.State with
        | Idle ->
            { stream with State = StreamState.Open }
            |> updateStream conn pos
            |> transitionIdle frame
        | Open -> failwith "todo"
        | Closed -> failwith "todo"
        | Reserved -> failwith "todo"
        | HalfClosed -> failwith "todo"

    let Empty =
        { Streams = List.Empty; HpackTable = Table.empty; FieldBlock = emptyFieldBlock }
