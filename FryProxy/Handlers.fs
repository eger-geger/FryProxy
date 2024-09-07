module FryProxy.Handlers

open System.Net.Http
open System.Threading.Tasks
open FryProxy.Http
open FryProxy.Extension
open FryProxy.Http.Fields

/// Handles CONNECT requests by establishing a tunnel using provided factory.
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

/// Drops 'Connection' request field and report whether it requested termination.
let requestConnectionHeader (close: bool ref) req (next: RequestHandler) =
    let (Message(Header(requestLine, requestFields), requestBody)) = req

    match Connection.TryDrop requestFields with
    | Some(conn: Connection), requestFields' ->
        close.Value <- conn.IsClose

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
        close.Value <- false
        next.Invoke req

/// Drops 'Connection' response field and report whether it requested termination.
let responseConnectionHeader (close: bool ref) req (next: RequestHandler) =
    ValueTask.FromTask
    <| task {
        let! Message(Header(line, fields), body) as resp = next.Invoke req
        
        match Connection.TryDrop fields with
        | Some(conn: Connection), fields' ->
            close.Value <- conn.IsClose
            return Message(Header(line, fields'), body)
        | _ ->
           close.Value <- false
           return resp 
    }