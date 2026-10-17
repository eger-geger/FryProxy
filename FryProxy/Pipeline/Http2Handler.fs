module FryProxy.Pipeline.Http2Handler

open System.Net.Http
open System.Threading.Tasks
open FryProxy.Http
open FryProxy.Http2
open System.Net
open FryProxy.Http.Fields
open FryProxy.Http2.Fields
open FryProxy.Http2.Frames
open FryProxy.Http2.Hpack
open FryProxy.IO
open FryProxy.IO.BufferedParser
open FryProxy.Pipeline.Failures
open FryProxy.Pipeline.RequestHandler


let tryParseConnectionFrame (state: ConnectionState voption) : ConnectionStateTransition voption Parser =
    let cnx = state |> ValueOption.defaultValue ServerConnection.Empty

    Parse.frame
    |> Parser.map (ServerConnection.transition cnx)
    |> Parser.map ValueSome



/// Read a request, run it through a pipeline to produce a response and write it back.
/// Return response context produced by the pipeline as a result.
let executePipelineIO (pipeline: 'ctx RequestHandler) (clientBuffer: ReadBuffer) =
    task {
        let! frameSeq = Parser.unfold tryParseConnectionFrame |> Parser.run clientBuffer
        let frameEnum = frameSeq.GetAsyncEnumerator()

        while! frameEnum.MoveNextAsync() do
            match frameEnum.Current with
            | None -> do ()
            | ConnectionError code -> failwith "todo"
            | StreamError(streamId, code) -> failwith "todo"
            | PingRequest bytes -> failwith "todo"
            | MessageBody stream -> failwith "todo"
            | MessageFields fields -> failwith "todo"
            | StreamReset code -> failwith "todo"
            | ConnectionClose code -> failwith "todo"
            | ConnectionWindowUpdate increment -> failwith "todo"
            | StreamWindowUpdate(streamId, increment) -> failwith "todo"
            | SettingsAck -> failwith "todo"
            | ClientSettings settings -> failwith "todo"

        failwith "Not implemented"
    }

let tryDecodeRequestLine fields =
    let path, rem = TryPop<Path> fields
    let method, rem = TryPop<Method> rem
    let authority, rem = TryPop<Authority> rem

    match struct (method, authority, path) with
    | Some({ Method = m }, _), Some({ Authority = a }, _), Some({ Path = p }, _) when m <> HttpMethod.Connect ->
        Some({ Method = m; Target = p; Version = Protocol.Http2 }, FieldOf { Host = a } :: rem)
    | Some({ Method = m }, _), Some({ Authority = a }, _), Option.None when m = HttpMethod.Connect ->
        Some({ Method = m; Target = a; Version = Protocol.Http2 }, FieldOf { Host = a } :: rem)
    | Some({ Method = m }, _), Some({ Authority = a }, _), Option.None when m = HttpMethod.Options ->
        Some({ Method = m; Target = "*"; Version = Protocol.Http2 }, FieldOf { Host = a } :: rem)
    | _ -> Option.None

let createRequestMessage (fields: FieldPack List) : RequestMessage Option =
    let reqFields = fields |> List.map FieldPack.field

    reqFields
    |> tryDecodeRequestLine
    |> Option.map (fun (requestLine, fields) -> { Header = { StartLine = requestLine; Fields = fields }; Body = Empty })
