module FryProxy.Tests.Pipeline.Middleware.MaxForwardsTests

open System.Net
open System.Net.Http
open System.Text
open System.Threading.Tasks
open FryProxy.Http
open FryProxy.Http.Protocol
open FryProxy.Http.Fields
open FryProxy.IO
open FryProxy.Pipeline

open FsUnit
open NUnit.Framework

let reqBody =
    MemoryByteSeq(Encoding.ASCII.GetBytes "How far can we you push humankind?")

let traceRequest =
    { Header =
        { StartLine = { Version = Http11; Method = HttpMethod.Trace; Target = "http://proxy.org/" }
          Fields =
            [ FieldOf { MaxForwards = 1u }
              FieldOf(ContentType.TextPlain(Encoding.ASCII))
              FieldOf { ContentLength = uint64 reqBody.Memory.Length } ] }
      Message.Body = MessageBody.Sized reqBody }

let defaultResponse = Response.emptyStatus HttpStatusCode.OK

let echoHandler (req: RequestMessage) =
    (defaultResponse, ValueSome(req)) |> ValueTask.FromResult


[<Test>]
let ``invokes next handler with updated value`` () =
    task {
        let! resp, ctx = Middleware.maxForwards traceRequest echoHandler

        resp |> should equal defaultResponse

        ctx
        |> should equal (traceRequest |> Message.withFieldOf { MaxForwards = 0u } |> ValueSome)
    }

[<Test>]
let ``produces response when value is zero`` () =
    let req = traceRequest |> Message.withFieldOf { MaxForwards = 0u }

    task {
        let! resp, ctx = Middleware.maxForwards req echoHandler

        resp |> should equal (Response.trace req)
        ctx.IsNone |> should be True
    }

let notApplicableCases =
    [ traceRequest |> Message.withoutField NameOf<MaxForwards>
      { traceRequest with Header.StartLine.Method = HttpMethod.Get } ]

[<TestCaseSource(nameof notApplicableCases)>]
let ``delegates to next handler`` req =
    task {
        let! resp, ctx = Middleware.maxForwards req echoHandler

        resp |> should equal defaultResponse
        ctx |> should equal (req |> ValueSome)
    }
