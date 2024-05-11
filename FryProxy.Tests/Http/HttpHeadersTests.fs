namespace FryProxy.Tests

open FryProxy.Http
open NUnit.Framework

type HttpHeadersTests() =

    static member private parseValueCases =
        let case (line: string) (values: string list) =
            TestCaseData(line, ExpectedResult = values)

        seq {
            yield case "" List.empty
            yield case "application/json" [ "application/json" ]
            yield case "application/json " [ "application/json" ]
            yield case " application/json" [ "application/json" ]
            yield case "application/json , text/xml" [ "application/json"; "text/xml" ]
        }

    [<TestCaseSource(nameof HttpHeadersTests.parseValueCases)>]
    member this.testParseHeaderValue(value) = Field.decodeValues value

    static member private parseHeaderCases =
        let success (line: string) header =
            TestCaseData(line, ExpectedResult = Some(Field.create <|| header))

        let failure (line: string) =
            TestCaseData(line, ExpectedResult = None)

        seq {
            yield failure ""
            yield failure "Accept"
            yield failure "Accept:"

            yield success "Accept: application/json" <| ("Accept", [ "application/json" ])

            yield
                success "Accept: application/json,application/xml"
                <| ("Accept", [ "application/json"; "application/xml" ])
        }

    [<TestCaseSource(nameof HttpHeadersTests.parseHeaderCases)>]
    member this.testParseHeaderLine(line) = Field.tryDecode line
