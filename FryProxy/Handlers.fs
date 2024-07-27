module FryProxy.Handlers

open System.Net.Http
open System.Threading.Tasks
open FryProxy.Http
open FryProxy.Extension
open FryProxy.Http.Fields

/// Responds to CONNECT requests after establishing a tunnel.
let tunnel factory (result: _ ref) req (next: RequestHandler) =
    let (Message(Header({ Method = method }, _) as header, _)) = req

    if method = HttpMethod.Connect then
        ValueTask.FromTask
        <| task {
            let! resp, cxnOpt = Proxy.tunnel factory header

            match cxnOpt with
            | ValueSome v -> result.Value <- v
            | ValueNone -> result.Value <- null

            return resp
        }
    else
        next.Invoke req

/// Drops connection header field and based on its value determines whether client connection should be closed after
/// sending response.
let connectionHeader (result: bool ref) req (next: RequestHandler) =
    let (Message(Header(requestLine, requestFields), requestBody)) = req

    match Connection.TryDrop requestFields with
    | Some(conn: Connection), requestFields' ->
        result.Value <- conn.IsClose

        ValueTask.FromTask
        <| task {
            let! (Message(Header(statusLine, responseFields), responseBody)) =
                next.Invoke(Message(Header(requestLine, requestFields'), requestBody))

            let responseFields' =
                if conn.IsClose then
                    let (_: Connection Option, fields') = Connection.TryDrop responseFields
                    Connection.Close.ToField() :: fields'
                else
                    responseFields

            return Message(Header(statusLine, responseFields'), responseBody)
        }
    | _ ->
        result.Value <- false
        next.Invoke req
