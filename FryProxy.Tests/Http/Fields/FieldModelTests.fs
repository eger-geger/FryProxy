module FryProxy.Tests.Http.Fields.FieldModelTests

open FryProxy.Http
open FryProxy.Http.Fields

open NUnit.Framework

let dropConnectionCases =
    let makeTestCase (fields: Field list, dropIndex) =
        let retVal =
            match dropIndex with
            | Some i ->
                let popped =
                    List.item i fields |> Connection.FromField |> Option.map(fun f -> (f, i))

                popped, List.removeAt i fields
            | None -> Option<Connection * int>.None, fields

        TestCaseData(fields, ExpectedResult = retVal)

    let connField = Connection.CloseField
    let hostField = FieldOf { Host = "localhost" }
    let lenField = FieldOf { ContentLength = 22UL }

    Seq.map makeTestCase
    <| seq {
        yield [ hostField; connField ], Some(1)
        yield [ connField; hostField ], Some(0)
        yield [ hostField; connField; lenField ], Some(1)
        yield [ hostField; lenField ], None
    }

[<TestCaseSource(nameof dropConnectionCases)>]
let testTryDropField (fields: Field list) = TryPop<Connection> fields
