[<RequireQualifiedAccess>]
module FryProxy.Pipeline.Middleware

open System.IO
open System.Net
open System.Net.Http
open System.Threading.Tasks
open FryProxy.Http
open FryProxy.Extension
open FryProxy.Http.Fields

/// Conditionally apply one of the two handlers.
let inline whenMatch condition (a: RequestHandler) req (b: RequestHandler) =
    if condition req then
        a.Invoke req
    else
        b.Invoke req

/// Resolve a request target for another handler or respond with a bad request.
let resolveTarget (next: Target * RequestMessage -> ResponseMessage ValueTask) (req: RequestMessage) =
    match Request.tryResolveTarget req.Header with
    | ValueSome target -> next(target, req)
    | ValueNone -> HttpStatusCode.BadRequest |> Response.emptyStatus |> ValueTask.FromResult

/// Handles CONNECT requests by establishing a tunnel using provided factory.
let tunnel (result: _ ref) (factory: Target -> _ ValueTask) =
    whenMatch(fun req -> req.Header.StartLine.Method = HttpMethod.Connect)
    <| resolveTarget(fun (target, _) ->
        ValueTask.FromTask
        <| task {
            try
                let! tunnel = factory target
                result.Value <- tunnel
                return Response.empty 200us
            with
            | :? IOException -> return Response.emptyConnectionClose HttpStatusCode.BadGateway
            | _ -> return Response.emptyConnectionClose HttpStatusCode.InternalServerError
        })


/// Drops 'Connection' request field and report whether it requested termination.
let requestConnectionHeader (close: bool ref) req (next: RequestHandler) =
    match Connection.TryPop req.Header.Fields with
    | Some(conn: Connection), requestFields ->
        close.Value <- conn.IsClose

        ValueTask.FromTask
        <| task {
            let! resp = next.Invoke({ req with Header.Fields = requestFields })

            let responseFields' =
                if conn.IsClose then
                    let (_: Connection Option, fields') = Connection.TryPop resp.Header.Fields
                    Connection.Close.ToField() :: fields'
                else
                    resp.Header.Fields

            return { resp with Header.Fields = responseFields' }
        }
    | _ ->
        close.Value <- false
        next.Invoke req

/// Drops 'Connection' response field and report whether it requested termination.
let responseConnectionHeader (close: bool ref) req (next: RequestHandler) =
    ValueTask.FromTask
    <| task {
        let! resp = next.Invoke req

        match Connection.TryPop resp.Header.Fields with
        | Some(conn: Connection), fields' ->
            close.Value <- conn.IsClose
            return { resp with Header.Fields = fields' }
        | _ ->
            close.Value <- false
            return resp
    }
