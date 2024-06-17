module FryProxy.Proxy

open System.Net
open System.Text
open System.Threading.Tasks
open FryProxy.Extension
open FryProxy.Http
open FryProxy.IO
open FryProxy.IO.BufferedParser
open Microsoft.FSharp.Core

exception RequestError of string

let requestErr msg =
    ValueTask.FromException<'a>(RequestError msg)

let badRequest =
    Response.plainText(uint16 HttpStatusCode.BadRequest) >> ValueTask.FromResult

/// Resolve request destination server or raise RequestError
let resolveTarget (header: RequestHeader) =
    match Request.tryResolveTarget header with
    | ValueSome res -> res
    | ValueNone -> raise(RequestError "Unable to determine requested resource based on header")

/// Send request to it's original destination and parse response message.
let reverse connect (Message(header, _) as request) =
    ValueTask.FromTask
    <| task {
        let! (rb: ReadBuffer) = resolveTarget header |> connect

        do! Message.write request rb.Stream

        try
            return! Parse.response |> Parser.run rb
        with ParseError _ as err ->
            return! badRequest $"Unable to parse response headers: {err}"
    }

/// Create SSL tunnel to request destination and acknowledge that to a client.
let tunnel connect (Message(header, _)) =
    ValueTask.FromTask
    <| task {
        do! connect (resolveTarget header)
        return Response.empty 200us
    }

/// Parse incoming request and respond to it using handler.
let respond (handler: RequestMessage -> ResponseMessage ValueTask) (rb: ReadBuffer) =
    backgroundTask {
        let! response =
            task {
                try
                    let! Message(header, _) as request = Parse.request |> Parser.run rb

                    try
                        return! handler request
                    with err ->
                        return!
                            StringBuilder($"Failed to handle request: {err}")
                                .AppendLine(String.replicate 40 "-")
                                .AppendLine(header.ToString())
                                .ToString()
                            |> badRequest
                with ParseError _ as err ->
                    return! badRequest $"Unable to parse request header: {err}"
            }

        do! Message.write response rb.Stream
    }
