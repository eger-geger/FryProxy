namespace FryProxy.Tests

open FryProxy.Http
open NUnit.Framework
open FryProxy.Http.Header

type HttpHeadersTests() =

    static member private headerValueTestCases =
        seq {
            yield TestCaseData("").Returns(List.empty<string>)

            yield
                TestCaseData("application/json")
                    .Returns([ "application/json" ])

            yield
                TestCaseData("application/json ")
                    .Returns([ "application/json" ])

            yield
                TestCaseData(" application/json")
                    .Returns([ "application/json" ])

            yield
                TestCaseData("application/json , text/xml")
                    .Returns([ "application/json"; "text/xml" ])
        }

    [<TestCaseSource("headerValueTestCases")>]
    member this.testParseHeaderValue(value) = parseValue value

    static member headerLineTestCases =
        seq {
            yield TestCaseData("").Returns(None)
            yield TestCaseData("Accept").Returns(None)
            yield TestCaseData("Accept:").Returns(None)

            yield
                TestCaseData("Accept: application/json")
                    .Returns(Some(create "Accept" [ "application/json" ]))

            yield
                TestCaseData("Accept: application/json,application/xml")
                    .Returns(Some(create "Accept" [ "application/json"; "application/xml" ]))
        }

    [<TestCaseSource("headerLineTestCases")>]
    member this.testParseHeaderLine(line) = tryParse line
