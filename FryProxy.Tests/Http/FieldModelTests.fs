module FryProxy.Tests.Http.FieldModelTests

open FryProxy.Http
open FryProxy.Http.Fields

open NUnit.Framework

let dropConnectionCases =
    let makeTestCase (fields: Field list, dropIndex) =
        let retVal =
            match dropIndex with
            | Some i ->
                let fld = List.item i fields
                Connection.FromField(fld), List.removeAt i fields
            | None -> Option<Connection>.None, fields

        TestCaseData(fields, ExpectedResult = retVal)

    let connField = Connection.Close.ToField()
    let hostField = { Host = "localhost" }.ToField()
    let lenField = { ContentLength = 22UL }.ToField()

    Seq.map makeTestCase
    <| seq {
        yield [ hostField; connField ], Some(1)
        yield [ connField; hostField ], Some(0)
        yield [ hostField; connField; lenField ], Some(1)
        yield [ hostField; lenField ], None
    }

[<TestCaseSource(nameof dropConnectionCases)>]
let testTryDropField (fields: Field list) : Connection option * Field list = Connection.TryPop fields
