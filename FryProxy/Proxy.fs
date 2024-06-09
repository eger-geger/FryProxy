module FryProxy.Proxy

open System.Net
open System.Text
open System.Threading.Tasks
open FryProxy.Http
open FryProxy.IO
open FryProxy.IO.BufferedParser
open Microsoft.FSharp.Core

let badRequest =
    Response.plainText(uint16 HttpStatusCode.BadRequest) >> ValueTask.FromResult

/// Resolve requested resource, send it the request and parse response.
/// Apply handler to the response and return the result.
let executeRequest connect (transform: ResponseMessage -> ResponseMessage ValueTask) (Message(header, _) as request) =
    let proxyResource (res: Resource) =
        task {
            let! (rb: ReadBuffer) = connect(res.Host, res.Port)

            do! Message.write request rb.Stream

            try
                let! Message(header, _) as response = Parse.response |> Parser.run rb

                try
                    return! transform response
                with err ->
                    return!
                        StringBuilder($"Failed to transform response: {err}")
                            .AppendLine(String.replicate 40 "-")
                            .AppendLine(header.ToString())
                            .ToString()
                        |> badRequest
            with ParseError _ as err ->
                return! badRequest $"Unable to parse response headers: {err}"
        }
        |> ValueTask<ResponseMessage>

    match Request.tryResolveResource 80 header with
    | Some res -> proxyResource res
    | None -> badRequest $"Unable to determine requested resource based on header: {header}"

let proxyRequest (handler: RequestMessage -> ResponseMessage ValueTask) (rb: ReadBuffer) =
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
