namespace FryProxy.Tests.Http

open System
open NUnit.Framework

open FsUnit
open FsUnitTyped
open FryProxy.Http

type StatusLineTests() =

    static member private samples includeNone =
        let success (line: string) (minor, major) status reason =
            let statusLine =
                StatusLine.create
                <| Version(minor, major)
                <| status
                <| reason

            TestCaseData(line, Some(statusLine))

        let failure (line: string) = TestCaseData(line, None)

        seq {
            yield success "HTTP/1.1 200 OK" (1, 1) 200us "OK"
            yield success "HTTP/1.1 400 Bad Request" (1, 1) 400us "Bad Request"

            if includeNone then
                yield failure "HTTP/1.0 AAA Internal Server Error"
                yield failure "HTTP 500"
        }

    [<TestCaseSource(nameof StatusLineTests.samples, methodParams = [| true |])>]
    member this.testTryParse(line, statusLineOption) =
        StatusLine.tryParse line
        |> shouldEqual statusLineOption
    
    [<TestCaseSource(nameof StatusLineTests.samples, methodParams = [| false |])>]
    member this.testToString(line, statusLineOption) =
        Option.get statusLineOption
        |> StatusLine.toString
        |> shouldEqual line
    
    static member private invalidArguments =
        seq {
            yield TestCaseData(null, 200us, "OK")
            yield TestCaseData(Version(1, 0), 200us, null)
            yield TestCaseData(Version(1, 0), 200us, "")
        }

    [<TestCaseSource(nameof StatusLineTests.invalidArguments)>]
    member this.testCreateFailure version status reason =
        shouldFail (fun _ -> StatusLine.create version status reason |> ignore)

    [<TestCaseSource(typeof<ReasonPhraseCodes>, nameof ReasonPhraseCodes.supported)>]
    member this.testCreateDefault code =
        StatusLine.createDefault code
        |> should be instanceOfType<HttpStatusLine>
