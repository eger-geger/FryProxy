module FryProxy.Tests.Http.ResponsesTests

open System.Net.Http
open System.Text

open FryProxy.IO
open FryProxy.Http
open FryProxy.Http.Fields

open FsUnit
open NUnit.Framework

[<Test>]
let traceTest () =
    let req: RequestMessage =
        { Header =
            { StartLine = { Method = HttpMethod.Trace; Version = Protocol.Http11; Target = "/" }
              Fields =
                [ FieldOf { Host = "localhost" }
                  FieldOf { MaxForwards = 5u }
                  FieldOf { Via = [ { Protocol = "HTTP"; Name = "nginx"; Comment = "" } ] } ] }
          Body = MessageBody.Empty }

    let responseBody =
        "TRACE / HTTP/1.1\r\n\
         Host: localhost\r\n\
         MaxForwards: 5\r\n\
         Via: HTTP nginx\r\n\
         \r\n"
        |> Encoding.ASCII.GetBytes

    let resp: ResponseMessage =
        { Header =
            { StartLine = StatusLine.createDefault 200us
              Fields =
                [ FieldOf ContentType.MessageHttp
                  FieldOf { ContentLength = uint64 responseBody.Length } ] }
          Body = MessageBody.Sized(MemoryByteSeq(responseBody)) }

    Response.trace req |> should equal resp
