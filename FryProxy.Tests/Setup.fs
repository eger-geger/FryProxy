namespace FryProxy.Tests

#nowarn "0988"

open System
open System.Diagnostics
open System.IO
open System.Net.Http
open FsUnit.TopLevelOperators
open NUnit.Framework
open NUnit.Framework.Interfaces

[<SetUpFixture>]
type TracingSetupFixture() =

    let mutable listener: TextWriterTraceListener = null

    [<OneTimeSetUp>]
    member _.setup() =
        let location : string = System.Reflection.Assembly.GetExecutingAssembly().Location
        let assemblyDir = Path.GetDirectoryName(location)
        let rootDir = Directory.GetParent(assemblyDir).Parent.Parent.Parent.FullName
        let logDir = Path.Combine(rootDir, "logs")
        let timestamp = DateTime.Now.ToString("yyMMdd-HHmmss")
        let logPath = Path.Combine(logDir, $"proxy-{timestamp}.log")

        Directory.CreateDirectory(logDir) |> ignore

        listener <- new TextWriterTraceListener(logPath)
        listener.TraceOutputOptions <- TraceOptions.DateTime ||| TraceOptions.ThreadId
        Trace.Listeners.Add(listener) |> ignore

    [<OneTimeTearDown>]
    member _.teardown() =
        if isNull listener then
            ()
        else
            listener.Flush()
            listener.Close()
            Trace.Listeners.Remove(listener) |> ignore

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
            |> Option.bind (_.MediaType >> Option.ofObj)
            |> Option.map (fun mediaType ->
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


type TestNameDelimiterActionAttribute() =
    inherit TestActionAttribute()

    override _.BeforeTest(test: ITest) =
        Trace.TraceInformation($"--- TEST START: {test.Name} ---")

    override _.AfterTest(test: ITest) =
        Trace.TraceInformation($"--- TEST END: {test.Name} ---")

    override _.Targets = ActionTargets.Test


[<assembly: TestNameDelimiterAction>]
do ()
