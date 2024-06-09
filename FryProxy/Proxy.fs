module FryProxy.Proxy

open System.Net
open System.Net.Sockets
open System.Text
open System.Threading.Tasks
open FryProxy.Http
open FryProxy.IO.BufferedParser

let badRequest =
    Response.plainText(uint16 HttpStatusCode.BadRequest) >> ValueTask.FromResult

let handleResponse _ response _ = ValueTask.FromResult(response)

/// Resolve request destination resource, parse response header and
/// delegate further response processing to response handler.
let handleRequest (ctx: IContext) (Message(header, _) as request) _ =
    let proxyResource (res: Resource) =
        task {
            let! socket = ctx.ConnectAsync(res.Host, res.Port)
            let rb = ctx.AllocateBuffer(socket)

            do! Message.write request rb.Stream

            try
                let! Message(header, _) as response = Parse.response |> Parser.run rb

                try
                    return! ctx.ResponseHandler.Invoke(ctx, response, handleResponse)
                with err ->
                    return!
                        StringBuilder($"Handler ({ctx.ResponseHandler}) had failed to process response: {err}")
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
    | None -> badRequest $"Unable to determine requested resource based on request header: {header}"

let proxyHttp (ctx: IContext) (socket: Socket) =
    backgroundTask {
        let rb = ctx.AllocateBuffer(socket)

        let! response =
            task {
                try
                    let! Message(header, _) as request = Parse.request |> Parser.run rb

                    try
                        return! ctx.RequestHandler.Invoke(ctx, request, handleRequest)
                    with err ->
                        return!
                            StringBuilder($"Handler ({ctx.RequestHandler}) had failed to process request: {err}")
                                .AppendLine(String.replicate 40 "-")
                                .AppendLine(header.ToString())
                                .ToString()
                            |> badRequest
                with ParseError _ as err ->
                    return! badRequest $"Unable to parse request header: {err}"
            }

        do! Message.write response rb.Stream
    }
