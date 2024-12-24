namespace FryProxy.Tests.Http

open FryProxy.Http
open NUnit.Framework

type FieldTests() =

    static member private decodeValueCases =
        let case (line: string) (values: string list) =
            TestCaseData(line, ExpectedResult = values)

        seq {
            yield case "" List.empty
            yield case "," List.empty
            yield case ",  ," List.empty
            yield case "application/json" [ "application/json" ]
            yield case "application/json " [ "application/json" ]
            yield case " application/json" [ "application/json" ]
            yield case "application/json , text/xml" [ "application/json"; "text/xml" ]
            yield case "application/json , , text/xml" [ "application/json"; "text/xml" ]
        }

    [<TestCaseSource(nameof FieldTests.decodeValueCases)>]
    member this.testDecodeValue(value) = Field.splitValues value

    static member private decodeFieldCases =
        let success (line: string) header =
            TestCaseData(line, ExpectedResult = Some header)

        let failure (line: string) =
            TestCaseData(line, ExpectedResult = None)

        seq {
            yield failure ""
            yield failure ":Hello"
            yield failure "Accept"

            yield success "Accept:" ("Accept", "")
            yield success "Accept: " ("Accept", "")
            yield success "Accept: application/json" ("Accept", "application/json")
            yield success "Accept : application/json" ("Accept", "application/json")
            yield success " Accept : application/json" ("Accept", "application/json")

            yield
                success "Accept: application/json,application/xml"
                <| ("Accept", "application/json,application/xml")


        }

    [<TestCaseSource(nameof FieldTests.decodeFieldCases)>]
    member this.testSplitNameValue line = Field.trySplitNameValue line
