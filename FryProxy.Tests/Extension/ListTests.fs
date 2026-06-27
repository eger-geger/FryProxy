module FryProxy.Extension.Tests.ListTests

open FryProxy.Extension
open NUnit.Framework


let tryFindVTestCases =
    let none = ValueNone: int voption

    seq {
        yield TestCaseData([ 1 ]).Returns(none)
        yield TestCaseData([]: int list).Returns(none)
        yield TestCaseData([ 3; 4; 5 ]).Returns(ValueSome 3)
        yield TestCaseData([ 1; 4; 2 ]).Returns(ValueSome 4)
        yield TestCaseData([ 1; 2; 3 ]).Returns(ValueSome 3)
    }

[<TestCaseSource(nameof tryFindVTestCases)>]
let tryFindVTest (xs: int list) = List.tryFindV ((<) 2) xs


let replaceFirstTestCases =
    let empty = []: int list
    
    seq {
        yield TestCaseData(empty).Returns(empty)
        yield TestCaseData([ 1 ]).Returns(empty)
        yield TestCaseData([ 1; 2 ]).Returns(empty)
        yield TestCaseData([ 3; 4; 5 ]).Returns([ -3; 4; 5 ])
        yield TestCaseData([ 1; 4; 2 ]).Returns([ 1; -4; 2 ])
        yield TestCaseData([ 1; 2; 3 ]).Returns([ 1; 2; -3 ])
        yield TestCaseData([ 1; 3; 4 ]).Returns([ 1; -3; 4 ])
    }

[<TestCaseSource(nameof replaceFirstTestCases)>]
let replaceFirstTest (xs: int list) =
    let selector x = if x > 2 then ValueSome (-x) else ValueNone
    List.replaceFirst selector xs
