module FryProxy.Pipeline.Http2Handler

open System.Threading.Tasks
open FryProxy.Http
open FryProxy.Http2
open System.Net
open FryProxy.Http2.Frames
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
            | None -> failwith "todo"
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
