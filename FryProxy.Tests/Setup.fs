namespace FryProxy.Tests

open System
open System.Net.Http
open FsUnit.TopLevelOperators
open NUnit.Framework

[<SetUpFixture>]
type InitFsUnitFormatter() =
    inherit FSharpCustomMessageFormatter()


[<SetUpFixture>]
module HttpResponseFormatFixture =

    let format (r: HttpResponseMessage) : string =
        let firstLine = $"HTTP/{r.Version} {r.StatusCode} {r.ReasonPhrase}"
        let headers = r.Headers.ToString()

        let content =
            r.Content.Headers.ContentType
            |> Option.ofObj
            |> Option.bind(_.MediaType >> Option.ofObj)
            |> Option.map(fun mediaType ->
                match mediaType with
                | "text/plain"
                | "text/html"
                | "application/json" -> r.Content.ReadAsStringAsync().Result
                | unsupported -> unsupported)

        let delim = "----Response----\n"

        String.Join('\n', firstLine, headers, content) |> sprintf "\n%s%s%s" delim
        <| delim

    [<OneTimeSetUp>]
    let setup () =
        TestContext.AddFormatter<HttpResponseMessage>(fun o -> o :?> HttpResponseMessage |> format)
