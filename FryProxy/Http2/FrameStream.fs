module FryProxy.Http2.FrameStream

open System
open System.Buffers
open FryProxy.IO
open FryProxy.Extension
open FryProxy.Http2.Hpack
open FryProxy.Http2.Frames
open FryProxy.Http2.Frames.FrameFlags

[<Struct>]
type MessagePart =
    | Fields of Fields: FieldPack list
    | Body of Body: IByteBuffer

let inline errorSeq code = Seq.initInfinite(fun _ -> Error code)


let private collectContinuationFrames (frames: Frame seq) =
    seq {
        use e = frames.GetEnumerator()
        let mutable wrongFrame = false
        let mutable lastFragment = false

        while not(wrongFrame || lastFragment) && e.MoveNext() do
            match e.Current.Body with
            | Continuation { FieldFragment = fragment } ->
                do lastFragment <- Frame.hasFlag END_HEADERS e.Current
                yield Ok struct (lastFragment, fragment)
            | _ ->
                do wrongFrame <- true
                yield Error ErrorCode.PROTOCOL_ERROR
    }

let private readHeaderFields table (frames: Frame seq) =
    seq {
        match Seq.tryHead frames with
        | Some({ Body = Headers { FieldFragment = block } } as frame) when Frame.hasFlag END_HEADERS frame ->
            match Table.decodeFields table block.Span with
            | Ok(fields, table') -> yield Ok(ValueSome struct (Fields fields, table'))
            | Error _ -> yield Error ErrorCode.COMPRESSION_ERROR
        | Some({ Body = Headers { FieldFragment = fragment } }) ->
            
            let buff = ArrayBufferWriter()
            buff.Write(fragment.Span)

            yield Ok ValueNone
                
            use e = collectContinuationFrames frames |> _.GetEnumerator()

            while e.MoveNext() do
                match e.Current with
                | Ok(false, fragment) ->
                    do buff.Write(fragment.Span)
                    yield Ok ValueNone
                | Ok(true, fragment) ->
                    do buff.Write(fragment.Span)

                    match Table.decodeFields table buff.WrittenSpan with
                    | Ok(fields, table') -> yield Ok(ValueSome struct (Fields fields, table'))
                    | Error _ -> yield Error ErrorCode.COMPRESSION_ERROR
                | Error code -> yield Error code
        | Some _ -> yield Error ErrorCode.PROTOCOL_ERROR
        | None -> yield Error ErrorCode.INTERNAL_ERROR
    }

// let stream (frames: Frame seq) : Result<MessagePart, ErrorCode> seq =
//     seq {
//             let first = Seq.head frames
//
//             match first.Body with
//             | Headers({ FieldFragment = fragment }) ->
//
//
//         }
