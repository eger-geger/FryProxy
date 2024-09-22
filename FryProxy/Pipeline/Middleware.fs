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

/// Conditionally apply one of the two handlers.
let inline whenMatch condition (a: _ RequestHandler) req (b: _ RequestHandler) =
    if condition req then
        a.Invoke req
    else
        b.Invoke req

/// Resolve a request target for another handler or respond with a bad request.
let resolveTarget (next: Target * RequestMessage -> 'a ContextualResponse) (req: RequestMessage) =
    match Request.tryResolveTarget req.Header with
    | ValueSome target -> next(target, req)
    | ValueNone ->
        (HttpStatusCode.BadRequest |> Response.emptyStatus, new 'a())
        |> ValueTask.FromResult

/// Propagates established tunnel along response message.
type ('Tunnel, 'T) ITunnelAware when 'T :> ITunnelAware<'Tunnel, 'T> and 'T: (new: unit -> 'T) =
    /// Receives an established tunnel.
    abstract WithTunnel: 'Tunnel -> 'T

    /// Exposes established tunnel, if any.
    abstract member Tunnel: 'Tunnel voption

/// Handles CONNECT requests by establishing a tunnel using provided factory.
let tunnel<'Tunnel, 'T when 'T :> ITunnelAware<'Tunnel, 'T>> (factory: Target -> _ ValueTask) =
    whenMatch(fun req -> req.Header.StartLine.Method = HttpMethod.Connect)
    <| resolveTarget(fun (target, _) ->
        ValueTask.FromTask
        <| task {
            let ctx = new 'T()

            try
                let! tunnel = factory target
                return Response.empty 200us, ctx.WithTunnel tunnel
            with
            | :? IOException -> return Response.emptyConnectionClose HttpStatusCode.BadGateway, ctx
            | _ -> return Response.emptyConnectionClose HttpStatusCode.InternalServerError, ctx
        })

/// Propagates whether client wants to close a connection after receiving a response message.
type 'T IClientConnectionAware when 'T :> IClientConnectionAware<'T> =

    /// Record whether client wants to close a connection after receiving a response message.
    abstract WithCloseClientConnection: bool -> 'T

    /// Report whether client wants to close a connection after receiving a response message.
    abstract member CloseClientConnection: bool

/// Drops 'Connection' request field and report whether it requested termination.
let clientConnection req (next: #IClientConnectionAware<_> RequestHandler) =
    let httpVer = req.Header.StartLine.Version
    let connOpt, requestFields = TryPop<Connection> req.Header.Fields

    let close =
        match connOpt with
        | Some(conn: Connection) ->
            (httpVer = Version(1, 1) && conn.ShouldClose)
            || (httpVer = Version(1, 0) && not conn.ShouldKeep)
        | None -> httpVer = Version(1, 0)

    ValueTask.FromTask
    <| task {
        let! resp, ctx = next.Invoke({ req with Header.Fields = requestFields })
        let ctx' = ctx.WithCloseClientConnection close
        let fields = resp.Header.Fields |> TryPop<Connection> |> snd

        let fields' =
            if close && httpVer = Version(1, 1) then
                Connection.CloseField :: fields
            elif not close && httpVer = Version(1, 0) then
                Connection.KeepAliveField :: fields
            else
                fields

        return { resp with Header.Fields = fields'; Header.StartLine.version = httpVer }, ctx'
    }

/// Propagates weather upstream wants to close a connection after sending a response message.
type 'T IUpstreamConnectionAware when 'T :> IUpstreamConnectionAware<'T> =
    /// Record weather upstream wants to close a connection after sending a response message.
    abstract WithCloseUpstreamConnection: bool -> 'T

    /// Report weather upstream wants to close a connection after sending a response message.
    abstract member CloseUpstreamConnection: bool

/// Drops 'Connection' response field and report whether it requested termination.
let upstreamConnection req (next: #IUpstreamConnectionAware<_> RequestHandler) =
    ValueTask.FromTask
    <| task {
        let! resp, ctx = next.Invoke req

        let resp', close =
            match Connection.TryPop resp.Header.Fields with
            | Some(conn: Connection), fields' -> { resp with Header.Fields = fields' }, conn.ShouldClose
            | _ -> resp, false

        return resp', ctx.WithCloseUpstreamConnection close
    }

/// Add or update 'Via' response and request field.
let viaField hop req (next: _ RequestHandler) =
    let inline appendHop fields = Via.append hop fields

    ValueTask.FromTask
    <| task {
        let! resp, ctx = next.Invoke({ req with Header.Fields = appendHop req.Header.Fields })

        return { resp with Header.Fields = appendHop resp.Header.Fields }, ctx
    }
