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

/// Propagates whether client wants to reuse a connection after receiving a response message.
type 'T IClientConnectionAware when 'T :> IClientConnectionAware<'T> =

    /// Record whether client wants to reuse a connection after receiving a response message.
    abstract WithKeepClientConnection: bool -> 'T

    /// Report whether client wants to reuse a connection after receiving a response message.
    abstract member KeepClientConnection: bool

/// Drops 'Connection' request field and report whether it requested termination.
let clientConnection req (next: #IClientConnectionAware<_> RequestHandler) =
    let httpVer = req.Header.StartLine.Version
    let connField, requestFields = TryPop<Connection> req.Header.Fields

    let keepAlive =
        match connField with
        | Some(conn: Connection) ->
            (httpVer = Version(1, 1) && not conn.Close)
            || (httpVer = Version(1, 0) && conn.KeepAlive)
        | None -> httpVer = Version(1, 1)

    ValueTask.FromTask
    <| task {
        let! resp, ctx = next.Invoke({ req with Header.Fields = requestFields })
        let ctx' = ctx.WithKeepClientConnection keepAlive
        let fields = resp.Header.Fields |> TryPop<Connection> |> snd

        let fields' =
            if not keepAlive && httpVer = Version(1, 1) then
                Connection.CloseField :: fields
            elif keepAlive && httpVer = Version(1, 0) then
                Connection.KeepAliveField :: fields
            else
                fields

        return { resp with Header.Fields = fields'; Header.StartLine.version = httpVer }, ctx'
    }

/// Propagates weather upstream allows reusing connection after sending a response message.
type 'T IUpstreamConnectionAware when 'T :> IUpstreamConnectionAware<'T> =

    /// Record weather upstream allows reusing connection after sending a response message.
    abstract WithKeepUpstreamConnection: bool -> 'T

    /// Report weather upstream allows reusing connection after sending a response message.
    abstract member KeepUpstreamConnection: bool

/// Drops 'Connection' response field and report whether it requested termination.
let upstreamConnection req (next: #IUpstreamConnectionAware<_> RequestHandler) =
    ValueTask.FromTask
    <| task {
        let! resp, ctx = next.Invoke req

        let resp', keepAlive =
            match Connection.TryPop resp.Header.Fields with
            | Some(conn: Connection), fields' -> { resp with Header.Fields = fields' }, not conn.Close
            | _ -> resp, true

        return resp', ctx.WithKeepUpstreamConnection keepAlive
    }

/// Add or update 'Via' response and request field.
let viaField hop req (next: _ RequestHandler) =
    let inline appendHop fields = Via.append hop fields

    ValueTask.FromTask
    <| task {
        let! resp, ctx = next.Invoke({ req with Header.Fields = appendHop req.Header.Fields })

        return { resp with Header.Fields = appendHop resp.Header.Fields }, ctx
    }
