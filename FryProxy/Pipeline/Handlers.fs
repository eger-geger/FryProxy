module FryProxy.Pipeline.Handlers

open System
open System.IO
open System.Net
open System.Net.Sockets
open System.Threading.Tasks
open FryProxy.Http
open FryProxy.Http.Fields
open FryProxy.IO
open FryProxy.Extension
open FryProxy.IO.BufferedParser
open FryProxy.Pipeline.RequestHandler

/// Signals about expected failure in request processing pipeline mapped to HTTP status code.
exception ProxyFailure of HttpStatusCode

let failWithCode code = raise(ProxyFailure code)

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
                | :? IOTimeoutException -> failWithCode HttpStatusCode.GatewayTimeout
                | :? IOException -> failWithCode HttpStatusCode.BadGateway

                try
                    let! line = Parse.continueLine |> Parser.run serverBuffer
                    return ValueSome line
                with
                | :? ParseError -> return ValueNone
                | BufferReadError(:? IOTimeoutException) -> return failWithCode HttpStatusCode.GatewayTimeout
                | BufferReadError(:? IOException) -> return failWithCode HttpStatusCode.BadGateway
            }

        let transferBody (line: #StartLine) =
            task {
                use clientWriter = Message.writer clientBuffer.Stream
                do! clientWriter.WriteLineAsync(line.Encode())
                do! clientWriter.FlushAsync()

                try
                    do! Message.writeBody req.Body serverWriter
                with
                | BufferReadError(:? ReadTimeoutException) -> return failWithCode HttpStatusCode.RequestTimeout
                | BufferReadError(:? IOException) -> return failWithCode HttpStatusCode.BadRequest
                | :? WriteTimeoutException -> return failWithCode HttpStatusCode.GatewayTimeout
                | :? IOException -> return failWithCode HttpStatusCode.BadGateway
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
        | BufferReadError(:? ReadTimeoutException) -> return failWithCode HttpStatusCode.RequestTimeout
        | BufferReadError(:? IOException) -> return failWithCode HttpStatusCode.BadRequest
        | :? WriteTimeoutException -> return failWithCode HttpStatusCode.GatewayTimeout
        | :? IOException -> return failWithCode HttpStatusCode.BadGateway
    }

/// Choose how to write a request depending on "Expect" header field value.
let writeRequestResolvingExpectation (clientBuffer: ReadBuffer) (req: RequestMessage) (serverBuffer: ReadBuffer) =
    match TryFind<Expect> req.Header.Fields with
    | Some f when f.IsContinue -> writeRequestPrompt clientBuffer req serverBuffer
    | Some _ -> failWithCode HttpStatusCode.ExpectationFailed
    | None -> writeRequestPlain req serverBuffer

/// Parse response message from the buffer and convert IO and parsing errors to failures with a status code.
let readResponse buffer =
    task {
        try
            return! Parse.response |> Parser.run buffer
        with
        | BufferReadError(:? ReadTimeoutException) -> return failWithCode HttpStatusCode.GatewayTimeout
        | BufferReadError(:? IOException) -> return failWithCode HttpStatusCode.BadGateway
        | ParseError _ -> return failWithCode HttpStatusCode.BadGateway
    }

/// Parse request message from the buffer and convert IO and parsing errors to failures with a status code.
let readRequest buffer =
    task {
        try
            return! Parse.request |> Parser.run buffer
        with
        | BufferReadError(:? ReadTimeoutException) -> return failWithCode HttpStatusCode.RequestTimeout
        | BufferReadError(:? IOException) -> return failWithCode HttpStatusCode.BadRequest
        | ParseError _ -> return failWithCode HttpStatusCode.BadRequest
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
            | :? OperationCanceledException -> return failWithCode HttpStatusCode.GatewayTimeout
            | :? IOException as ie ->
                let code =
                    match ie.InnerException with
                    | :? SocketException as se when se.SocketErrorCode = SocketError.TimedOut ->
                        HttpStatusCode.GatewayTimeout
                    | _ -> HttpStatusCode.ServiceUnavailable

                return failWithCode code
        }

    let exchange (target, request) =
        ValueTask.FromTask
        <| task {
            try
                let! rb = tryConnect target
                do! writeRequest request rb
                return! readResponse rb
            with ProxyFailure(code) ->
                return Response.emptyConnectionClose code
        }

    exchange |> withContext |> Middleware.resolveTarget

/// Read a request, run it through a pipeline to produce a response and write it back.
/// Return response context produced by pipeline as a result. 
let executePipelineIO (pipeline: 'ctx RequestHandler) (clientBuffer: ReadBuffer) =
    let respond () =
        task {
            try
                let! request = readRequest clientBuffer
                return! pipeline.Invoke request
            with
            | ProxyFailure code -> return (Response.emptyConnectionClose code, new 'ctx())
            | _ -> return (Response.emptyConnectionClose HttpStatusCode.InternalServerError, new 'ctx())
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
            Middleware.clientConnection +> Middleware.upstreamConnection +> chain

        let handler = completeChain.Seal(reverseHandler)
        let! ctx = executePipelineIO handler clientBuffer

        if not ctx.KeepUpstreamConnection then
            do serverConn.Value.Close()

        return ctx
    }
