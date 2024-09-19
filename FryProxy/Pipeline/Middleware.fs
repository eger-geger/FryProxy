[<RequireQualifiedAccess>]
module FryProxy.Pipeline.Middleware

open System
open System.IO
open System.Net
open System.Net.Http
open System.Threading.Tasks
open FryProxy.Http
open FryProxy.Extension
open FryProxy.Http.Fields
open FryProxy.IO

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
                do result.Value <- tunnel
                return Response.empty 200us
            with
            | :? IOException -> return Response.emptyConnectionClose HttpStatusCode.BadGateway
            | _ -> return Response.emptyConnectionClose HttpStatusCode.InternalServerError
        })


/// Drops 'Connection' request field and report whether it requested termination.
let clientConnection (close: bool ref) req (next: RequestHandler) =
    let httpVer = req.Header.StartLine.Version
    let connOpt, requestFields = TryPop<Connection> req.Header.Fields

    close.Value <-
        match connOpt with
        | Some(conn: Connection) ->
            (httpVer = Version(1, 1) && conn.ShouldClose)
            || (httpVer = Version(1, 0) && not conn.ShouldKeep)
        | None -> httpVer = Version(1, 0)

    ValueTask.FromTask
    <| task {
        let! resp = next.Invoke({ req with Header.Fields = requestFields })
        let fields = resp.Header.Fields |> TryPop<Connection> |> snd

        let fields' =
            if close.Value && httpVer = Version(1, 1) then
                Connection.CloseField :: fields
            elif not close.Value && httpVer = Version(1, 0) then
                Connection.KeepAliveField :: fields
            else
                fields

        return { resp with Header.Fields = fields'; Header.StartLine.version = httpVer }
    }

/// Drops 'Connection' response field and report whether it requested termination.
let upstreamConnection (close: bool ref) req (next: RequestHandler) =
    ValueTask.FromTask
    <| task {
        let! resp = next.Invoke req

        match Connection.TryPop resp.Header.Fields with
        | Some(conn: Connection), fields' ->
            close.Value <- conn.ShouldClose
            return { resp with Header.Fields = fields' }
        | _ ->
            close.Value <- false
            return resp
    }

/// Add or update 'Via' response and request field.
let viaField hop req (next: RequestHandler) =
    let inline appendHop fields = Via.append hop fields

    ValueTask.FromTask
    <| task {
        let! resp = next.Invoke({ req with Header.Fields = appendHop req.Header.Fields })

        return { resp with Header.Fields = appendHop resp.Header.Fields }
    }
