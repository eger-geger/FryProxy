[<RequireQualifiedAccess>]
module FryProxy.Http.Proxy

open System.IO
open System.Net
open System.Text
open System.Threading.Tasks
open Microsoft.FSharp.Core

open FryProxy.IO
open FryProxy.Extension
open FryProxy.IO.BufferedParser

exception RequestError of string

let badRequest =
    Response.plainText(uint16 HttpStatusCode.BadRequest) >> ValueTask.FromResult

/// Resolve request destination server or raise RequestError
let resolveTarget (header: RequestHeader) =
    match Request.tryResolveTarget header with
    | ValueSome res -> res
    | ValueNone -> raise(RequestError "Unable to determine requested resource based on header")

/// Send request to it's original destination and parse response message.
let reverse (connect: Target -> ReadBuffer ValueTask) (req: RequestMessage) =
    ValueTask.FromTask
    <| task {
        let! (rb: ReadBuffer) = resolveTarget req.Header |> connect

        let! writeErr =
            task {
                try
                    do! Message.write req rb.Stream
                    return null
                with err ->
                    return err
            }

        try
            match writeErr with
            | null -> return! Parse.response |> Parser.run rb
            | :? ReadTimeoutException -> return Response.emptyStatus HttpStatusCode.RequestTimeout
            | :? WriteTimeoutException -> return Response.emptyStatus HttpStatusCode.GatewayTimeout
            | _ -> return Response.emptyStatus HttpStatusCode.BadGateway
        with
        | :? IOTimeoutException -> return Response.emptyStatus HttpStatusCode.GatewayTimeout
        | :? IOException
        | ParseError _ -> return Response.emptyStatus HttpStatusCode.BadGateway
    }

/// Create SSL tunnel to request destination and acknowledge that to a client.
let tunnel (factory: Target -> _ ValueTask) header =
    ValueTask.FromTask
    <| task {
        try
            let! conn = factory(resolveTarget header)
            return Response.empty 200us, ValueSome conn
        with
        | :? IOException -> return Response.emptyStatus HttpStatusCode.BadGateway, ValueNone
        | _ -> return Response.emptyStatus HttpStatusCode.InternalServerError, ValueNone
    }

/// Parse incoming request and respond to it using handler.
let respond (handler: RequestMessage -> ResponseMessage ValueTask) (rb: ReadBuffer) =
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
            | Error(ParseError _ as err) -> return! badRequest $"Unable to parse request header: {err}"
            | Error(:? ReadTimeoutException) -> return Response.emptyStatus HttpStatusCode.RequestTimeout
            | Error _ -> return Response.emptyStatus HttpStatusCode.InternalServerError
            | Ok req ->
                try
                    return! handler req
                with err ->
                    return!
                        StringBuilder($"Failed to handle request: {err}")
                            .AppendLine(String.replicate 40 "-")
                            .AppendLine(req.Header.ToString())
                            .ToString()
                        |> badRequest
        }

    task {
        let! request = parseRequest rb
        let! response = respond request
        do! Message.write response rb.Stream
    }
