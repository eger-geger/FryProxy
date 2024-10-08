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


/// Drop 'Connection' request field and determine whether client connection can be reused.
let clientConnection req (next: #IClientConnectionAware<_> RequestHandler) =
    let httpVer = req.Header.StartLine.Version
    let connField, requestFields = TryPop<Connection> req.Header.Fields

    let keepAlive = connField |> Option.map fst |> Connection.isReusable httpVer

    ValueTask.FromTask
    <| task {
        let! resp, ctx = next.Invoke({ req with Header.Fields = requestFields })
        let ctx' = ctx.WithKeepClientConnection keepAlive
        let fields = resp.Header.Fields |> TryPop<Connection> |> snd
        let connIdx = connField |> Option.map snd |> Option.defaultValue fields.Length

        let fields' =
            Connection.makeField httpVer keepAlive
            |> ValueOption.map(List.insertAt connIdx >> (|>) fields)
            |> ValueOption.defaultValue fields

        return { resp with Header.Fields = fields'; Header.StartLine.Version = httpVer }, ctx'
    }


/// Drop 'Connection' response field and determine whether upstream connection can be reused.
let upstreamConnection req (next: #IUpstreamConnectionAware<_> RequestHandler) =
    ValueTask.FromTask
    <| task {
        let! resp, ctx = next.Invoke req
        let httpVer = resp.Header.StartLine.Version
        let connField, respFields = TryPop<Connection> resp.Header.Fields

        let keepAlive = connField |> Option.map fst |> Connection.isReusable httpVer

        return { resp with Header.Fields = respFields }, ctx.WithKeepUpstreamConnection keepAlive
    }

/// Add or update 'Via' response and request field.
let viaField hop req (next: _ RequestHandler) =
    let inline appendHop fields = Via.append hop fields

    ValueTask.FromTask
    <| task {
        let! resp, ctx = next.Invoke({ req with Header.Fields = appendHop req.Header.Fields })

        return { resp with Header.Fields = appendHop resp.Header.Fields }, ctx
    }

/// Update 'MaxForwards' header field value in request message before invoking the next handler or produce
/// a trace response if value is zero.
let maxForwards (req: RequestMessage) (next: _ RequestHandler) =
    let method = req.Header.StartLine.Method

    if not(method = HttpMethod.Trace || method = HttpMethod.Options) then
        next.Invoke req
    else
        match TryPop<MaxForwards> req.Header.Fields with
        | Some({ MaxForwards = 0u }, _), _ -> req |> RequestHandler.withContext(Response.trace >> ValueTask.FromResult)
        | Some({ MaxForwards = n }, i), fields ->
            next.Invoke
                { req with
                    Header.Fields = List.insertAt i (FieldOf { MaxForwards = n - 1u }) fields }
        | None, _ -> next.Invoke req
