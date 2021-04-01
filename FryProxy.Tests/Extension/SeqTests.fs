namespace FryProxy.Tests

open NUnit.Framework


type SeqTests() =

    static member private decomposeTestCases =
        seq {
            yield TestCaseData([]).Returns(None, Seq.empty)
            yield TestCaseData(Seq.map id [ 1 ]).Returns(Some(1), Seq.empty)
            yield TestCaseData(Seq.map id [ 1; 2 ]).Returns(Some(1), [ 2 ])
        }

    [<TestCaseSource(nameof SeqTests.decomposeTestCases)>]
    member this.testDecompose seq = Seq.decompose seq
    
    static member private consTestCases =
        seq {
            yield TestCaseData(12, Seq.empty).Returns([12])
            yield TestCaseData(12, [11]).Returns([12; 11])
        }
    
    [<TestCaseSource(nameof SeqTests.consTestCases)>]
    member this.testCons head tail = Seq.cons head tail