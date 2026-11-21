module FryProxy.Tests.Http2.ServerConnection.AdjacentStreamsTests

open FryProxy.Http2
open NUnit.Framework

let isAdjacentStreamsTestCases =
    let tc name (a: uint32) (b: uint32) (expected: bool) =
        TestCaseData(a, b, ExpectedResult = expected, TestName = name)

    let positive name a b = tc name a b true
    let negative name a b = tc name a b false

    [ positive "consecutive stream IDs: 1 and 3" 1u 3u
      positive "symmetric: 3 and 1" 3u 1u

      negative "same stream ID: 1 and 1" 1u 1u
      negative "non-adjacent gap: 1 and 5" 1u 5u

      positive "max stream ID and adjacent" 4294967295u 4294967293u
      positive "symmetric max adjacent" 4294967293u 4294967295u ]

[<TestCaseSource(nameof isAdjacentStreamsTestCases)>]
let testAdjacentStreams a b = ServerConnection.isAdjacentStreams a b
