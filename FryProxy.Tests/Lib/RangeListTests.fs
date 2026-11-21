module FryProxy.Tests.Lib.RangeListTests

open FryProxy.Lib
open NUnit.Framework

let adjacent (a: int) (b: int) = abs (a - b) = 1

let containsTestCases =
    let tc name (ranges: int range list) (value: int) (expected: bool) =
        TestCaseData(ranges, value, ExpectedResult = expected, TestName = name)

    let positive name ranges value = tc name ranges value true
    let negative name ranges value = tc name ranges value false

    [ negative "empty list contains nothing" [] 0

      negative "single range: before" [ (1, 5) ] 0
      positive "single range: at start" [ (1, 5) ] 1
      positive "single range: middle" [ (1, 5) ] 3
      positive "single range: at end" [ (1, 5) ] 5
      negative "single range: after" [ (1, 5) ] 6

      negative "two ranges: in gap" [ (1, 3); (7, 10) ] 5
      positive "two ranges: second start" [ (1, 3); (7, 10) ] 7
      negative "two ranges: early exit before first" [ (1, 3); (7, 10) ] 0 ]

[<TestCaseSource(nameof containsTestCases)>]
let testContains ranges value = RangeList.contains ranges value

let addTestCases =
    let tc name (ranges: int range list) (value: int) (expected: int range list) =
        TestCaseData(ranges, value, ExpectedResult = expected, TestName = name)

    [ tc "add to empty" [] 5 [ (5, 5) ]

      tc "add existing value" [ (5, 5) ] 5 [ (5, 5) ]

      tc "add adjacent before" [ (5, 5) ] 4 [ (4, 5) ]
      tc "add adjacent after" [ (5, 5) ] 6 [ (5, 6) ]

      tc "add non-adjacent before" [ (5, 5) ] 3 [ (3, 3); (5, 5) ]
      tc "add non-adjacent after" [ (5, 5) ] 8 [ (5, 5); (8, 8) ]

      tc "add in gap" [ (1, 3); (7, 10) ] 5 [ (1, 3); (5, 5); (7, 10) ]

      tc "add bridging value" [ (1, 3); (5, 7) ] 4 [ (1, 7) ]

      tc "add adjacent to first" [ (1, 3); (7, 10) ] 4 [ (1, 4); (7, 10) ]
      tc "add adjacent to second" [ (1, 3); (7, 10) ] 6 [ (1, 3); (6, 10) ]

      tc "add inside existing range" [ (1, 3); (7, 10) ] 2 [ (1, 3); (7, 10) ]

      tc "add adjacent before first" [ (1, 3); (7, 10) ] 0 [ (0, 3); (7, 10) ]
      tc "add adjacent after last" [ (1, 3); (7, 10) ] 11 [ (1, 3); (7, 11) ] ]

[<TestCaseSource(nameof addTestCases)>]
let testAdd ranges value = RangeList.add adjacent ranges value

let consecutiveAddTestCases =
    let tc name (values: int list) (expected: int range list) =
        TestCaseData(values, ExpectedResult = expected, TestName = name)

    [ tc "consecutive values merge into one range" [ 0; 1; 2; 3 ] [ (0, 3) ]

      tc "gapped values remain separate" [ 0; 2; 5 ] [ (0, 0); (2, 2); (5, 5) ]

      tc "partial merge" [ 0; 1; 4; 5 ] [ (0, 1); (4, 5) ] ]

[<TestCaseSource(nameof consecutiveAddTestCases)>]
let testConsecutiveAdd values =
    List.fold (RangeList.add adjacent) [] values
