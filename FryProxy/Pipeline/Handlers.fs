module FryProxy.Pipeline.Handlers

open System
open System.IO
open System.Net
open System.Text
open System.Threading.Tasks
open FryProxy.Http
open FryProxy.IO
open FryProxy.Extension
open FryProxy.IO.BufferedParser
open FryProxy.Pipeline.RequestHandler


let badRequest =
    Response.plainText(uint16 HttpStatusCode.BadRequest) >> ValueTask.FromResult

/// <summary> Send request to original destination and read response. </summary>
/// <param name="connect"> Establishes buffered connection to request target. </param>
/// <returns> Response message. </returns>
let reverseProxy (connect: Target -> ReadBuffer ValueTask) =
    let writeRequest req stream =
        task {
            try
                do! Message.write req stream
                return Ok()
            with err ->
                return Error(err)
        }

    let exchange (target, req) =
        ValueTask.FromTask
        <| task {
            let! responseBuff = connect target

            try
                match! writeRequest req responseBuff.Stream with
                | Error(:? ReadTimeoutException) -> return Response.emptyConnectionClose HttpStatusCode.RequestTimeout
                | Error(:? WriteTimeoutException) -> return Response.emptyConnectionClose HttpStatusCode.GatewayTimeout
                | Error _ -> return Response.emptyConnectionClose HttpStatusCode.BadGateway
                | Ok _ -> return! Parse.response |> Parser.run responseBuff
            with
            | :? IOTimeoutException -> return Response.emptyConnectionClose HttpStatusCode.GatewayTimeout
            | :? IOException
            | ParseError _ -> return Response.emptyConnectionClose HttpStatusCode.BadGateway
        }

    exchange |> withContext |> Middleware.resolveTarget

/// Read request and execute it with a pipeline, writing the response back and returning accompanied context.
let serveHttpMessage (pipeline: 'ctx RequestHandler) (clientBuffer: ReadBuffer) =
    let parseRequest rb =
        task {
            try
                let! request = Parse.request |> Parser.run rb
                return Ok(request)
            with err ->
                return Error(err)
        }

    let respond request =
        task {
            match request with
            | Error(ParseError _ as err) -> return! withContext badRequest $"Unable to parse request header: {err}"
            | Error(:? ReadTimeoutException) -> return Response.emptyStatus HttpStatusCode.RequestTimeout, new 'ctx()
            | Error _ -> return Response.emptyStatus HttpStatusCode.InternalServerError, new 'ctx()
            | Ok req ->
                try
                    return! pipeline.Invoke req
                with err ->
                    return!
                        StringBuilder($"Failed to handle request: {err}")
                            .AppendLine(String.replicate 40 "-")
                            .AppendLine(req.Header.ToString())
                            .ToString()
                        |> withContext badRequest
        }

    task {
        let! request = parseRequest clientBuffer
        let! response, ctx = respond request
        do! Message.write response clientBuffer.Stream
        return ctx
    }

/// <summary>
/// Serve the incoming request passing it through the chain of handlers using reverse proxy as the innermost one.
/// </summary>
/// <param name="connect">used by reverse proxy to establish a connection to request target</param>
/// <param name="chain">intermediate handlers</param>
/// <param name="clientBuffer">buffered client stream</param>
/// <returns>flag indicating whether client connection can remain open</returns>
let proxyHttpMessage (connect: Target -> IConnection ValueTask) (chain: _ RequestHandlerChain) clientBuffer =
    task {
        let serverConn = ref Connection.Empty

        use connectionScope =
            { new IDisposable with
                member _.Dispose() = serverConn.Value.Dispose() }

        do ignore connectionScope

        let establishScopedConnection target =
            ValueTask.FromTask
            <| task {
                let! conn = connect target
                serverConn.Value <- conn
                return conn.Buffer
            }

        let handler =
            Middleware.clientConnection +> Middleware.upstreamConnection +> chain
            |> _.Seal(reverseProxy establishScopedConnection)

        let! ctx = serveHttpMessage handler clientBuffer

        if not ctx.KeepUpstreamConnection then
            do serverConn.Value.Close()

        return ctx
    }
