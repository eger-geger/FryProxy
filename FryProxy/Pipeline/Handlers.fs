module FryProxy.Pipeline.Handlers

open System
open System.IO
open System.Net
open System.Net.Sockets
open System.Text
open System.Threading.Tasks
open FryProxy.Http
open FryProxy.Http.Fields
open FryProxy.IO
open FryProxy.Extension
open FryProxy.IO.BufferedParser
open FryProxy.Pipeline.RequestHandler

/// Signals about expected failure in request processing pipeline mapped to HTTP status code.
exception ProxyFailure of code: HttpStatusCode * message: string

let inline failProxy code msg = raise(ProxyFailure(code, msg))

let inline gatewayTimeout operation =
    failProxy HttpStatusCode.GatewayTimeout $"Timed out {operation}"

let inline badGateway msg = failProxy HttpStatusCode.BadGateway msg

let inline serviceUnavailable msg =
    failProxy HttpStatusCode.ServiceUnavailable msg

let inline badRequest msg = failProxy HttpStatusCode.BadRequest msg

let inline requestTimeout operation =
    failProxy HttpStatusCode.RequestTimeout $"Timed out {operation}"

let fmtFailure (operation: string) (err: exn) =
    StringBuilder()
        .AppendLine($"Failed {operation}")
        .Append("\t")
        .AppendLine($"{err.GetType().Name}: {err.Message}")
        .ToString()

#nowarn "0064"
let inline connectionAware () : #IClientConnectionAware<'T> & #IUpstreamConnectionAware<'T> = new 'T()

let inline failureResponse code message =
    let ctx = connectionAware()
    let resp = Response.plainText (uint16 code) message

    match resp.Header.StartLine.Code with
    | status when status >= 500us -> resp, ctx.WithKeepUpstreamConnection false
    | status when status >= 400us -> Message.withField Connection.CloseField resp, ctx.WithKeepClientConnection false
    | _ -> resp, ctx

/// Prompt before sending request body and send it only after receiving the confirmation ("Continue").
/// Also convert IO and parsing errors to failures with a status code.
let writeRequestPrompt (clientBuffer: ReadBuffer) (req: RequestMessage) (serverBuffer: ReadBuffer) =
    task {
        use serverWriter = Message.writer serverBuffer.Stream

        let promptServer () =
            task {
                try
                    do! Message.writeHeader req.Header serverWriter
                    do! serverWriter.FlushAsync()
                with
                | :? IOTimeoutException -> do gatewayTimeout "sending request header"
                | :? IOException as err -> do serviceUnavailable <| fmtFailure "sending request header" err

                try
                    let! line = Parse.continueLine |> Parser.run serverBuffer
                    return ValueSome line
                with
                | :? ParseError -> return ValueNone
                | BufferReadError(:? IOTimeoutException) -> return gatewayTimeout "reading 'Continue' line"
                | BufferReadError(:? IOException as err) ->
                    return serviceUnavailable <| fmtFailure "reading 'Continue' line" err
            }

        let transferBody (line: StatusLine) =
            task {
                use clientWriter = Message.writer clientBuffer.Stream
                do! clientWriter.WriteLineAsync(StartLine.encode line)
                do! clientWriter.WriteLineAsync()
                do! clientWriter.FlushAsync()

                try
                    do! Message.writeBody req.Body serverWriter
                with
                | ParseError err -> return badRequest $"Malformed request body: {err}"
                | BufferReadError(:? ReadTimeoutException) -> return requestTimeout "reading request body"
                | BufferReadError(:? IOException as err) -> return badRequest <| fmtFailure "reading request body" err
                | :? WriteTimeoutException -> return gatewayTimeout "sending request body"
                | :? IOException as err -> return badGateway <| fmtFailure "sending request body" err
            }

        match! promptServer() with
        | ValueNone -> return ()
        | ValueSome line -> return! transferBody(line)
    }

/// Write request message to buffered stream and convert IO errors to failures with a status code.
let writeRequestPlain (req: RequestMessage) (serverBuff: ReadBuffer) =
    task {
        try
            do! Message.write req serverBuff.Stream
        with
        | ParseError err -> return badRequest $"Malformed request body: {err}"
        | BufferReadError(:? ReadTimeoutException) -> return requestTimeout "reading request body"
        | BufferReadError(:? IOException as err) -> return badRequest <| fmtFailure "reading request body" err
        | :? WriteTimeoutException -> return gatewayTimeout "sending request body"
        | :? IOException as err -> return badGateway <| fmtFailure "sending request body" err
    }

/// Choose how to write a request depending on "Expect" header field value.
let writeRequestResolvingExpectation (clientBuffer: ReadBuffer) (req: RequestMessage) (serverBuffer: ReadBuffer) =
    match TryFind<Expect> req.Header.Fields with
    | Some f when f.IsContinue -> writeRequestPrompt clientBuffer req serverBuffer
    | Some f -> failProxy HttpStatusCode.ExpectationFailed $"Unsupported 'Expect' values: {f.Expect}"
    | None -> writeRequestPlain req serverBuffer

/// Parse response message from the buffer and convert IO and parsing errors to failures with a status code.
let readResponse buffer =
    ValueTask.FromTask
    <| task {
        try
            return! Parse.response |> Parser.run buffer
        with
        | BufferReadError(:? ReadTimeoutException) -> return gatewayTimeout "reading response"
        | BufferReadError(:? IOException as err) -> return badGateway <| fmtFailure "reading response" err
        | ParseError err -> return badGateway $"Received malformed HTTP response message: {err}"
    }

/// Parse request message from the buffer and convert IO and parsing errors to failures with a status code.
let readRequest buffer =
    task {
        try
            return! Parse.request |> Parser.run buffer
        with
        | BufferReadError(:? ReadTimeoutException) -> return requestTimeout "reading request"
        | BufferReadError(:? IOException as err) -> return badRequest <| fmtFailure "reading request" err
        | ParseError err -> return badRequest $"Received malformed HTTP request message: {err}"
    }

/// <summary> Send request to original destination and read response. </summary>
/// <param name="connect"> Establishes buffered connection to request target. </param>
/// <param name="writeRequest"> Writes request message to server buffered stream. </param>
/// <returns> Response message. </returns>
let reverseProxy (connect: Target -> ReadBuffer ValueTask) writeRequest =
    let tryConnect target =
        task {
            try
                return! connect target
            with
            | :? OperationCanceledException -> return gatewayTimeout $"connecting to {target}"
            | :? IOException as ier ->
                match ier.InnerException with
                | :? SocketException as se when se.SocketErrorCode = SocketError.TimedOut ->
                    return gatewayTimeout $"connecting to {target}"
                | _ -> return serviceUnavailable <| fmtFailure $"connecting to {target}" ier
        }

    let exchange (target, request) =
        ValueTask.FromTask
        <| task {
            try
                let! rb = tryConnect target
                do! writeRequest request rb
                return! (readResponse |> withContext) rb
            with ProxyFailure(code, msg) ->
                return failureResponse code msg
        }

    Middleware.resolveTarget exchange

/// Read a request, run it through a pipeline to produce a response and write it back.
/// Return response context produced by pipeline as a result.
let executePipelineIO (pipeline: 'ctx RequestHandler) (clientBuffer: ReadBuffer) =
    let respond () =
        task {
            try
                let! request = readRequest clientBuffer
                return! pipeline.Invoke request
            with
            | ProxyFailure(code, msg) -> return failureResponse code msg
            | err ->
                return
                    failureResponse
                    <| HttpStatusCode.InternalServerError
                    <| fmtFailure "processing request" err
        }

    task {
        let! response, ctx = respond()
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

        let reverseHandler =
            reverseProxy
            <| establishScopedConnection
            <| writeRequestResolvingExpectation clientBuffer

        let completeChain =
            Middleware.requestVersion
            +> Middleware.clientConnection
            +> Middleware.upstreamConnection
            +> chain

        let handler = completeChain.Seal(reverseHandler)
        let! ctx = executePipelineIO handler clientBuffer

        if not ctx.KeepUpstreamConnection then
            do serverConn.Value.Close()

        return ctx
    }
