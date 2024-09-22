module FryProxy.Tests.Pipeline.Middleware.ResolveTargetTests

open System.Net.Http
open System.Threading.Tasks
open FryProxy.Http
open FryProxy.Http.Fields
open FryProxy.Pipeline

open NUnit.Framework
open FsUnit

let echoTarget req (target: Target, req') =
    let status = if req' = req then 200us else 400us

    ValueTask.FromResult<ResponseMessage>
    <| { Header =
           { StartLine = StatusLine.createDefault status
             Fields = [ FieldOf { Host = target.ToString() } ] }
         Body = MessageBody.Empty }

[<TestCase("example.org")>]
[<TestCase("example.org:8080")>]
let ``passes-on resolved target and request`` (host: string) =
    let req: RequestMessage =
        { Header =
            { StartLine = RequestLine.create11 HttpMethod.Get $"http://{host}/"
              Fields = [] }
          Body = MessageBody.Empty }

    let handler = echoTarget req |> Handlers.initContext |> Middleware.resolveTarget

    task {
        let! resp, _ = handler req

        resp
        |> should
            equal
            { Message.Header =
                { StartLine = StatusLine.createDefault 200us
                  Fields = [ FieldOf { Host = host } ] }
              Body = MessageBody.Empty }
    }

[<Test>]
let ``responds with bad request when request target cannot be inferred`` () =
    let req: RequestMessage =
        { Header = { StartLine = RequestLine.create11 HttpMethod.Get "/"; Fields = [] }
          Body = MessageBody.Empty }

    let handler = echoTarget req |> Handlers.initContext |> Middleware.resolveTarget

    task {
        let! resp, _ = handler req

        resp
        |> should
            equal
            { Message.Header = { StartLine = StatusLine.createDefault 400us; Fields = [] }
              Body = MessageBody.Empty }
    }
