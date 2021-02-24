namespace FryProxy.Tests

open NUnit.Framework


type SeqTests() =

    static member private decomposeTestCases =
        seq {
            yield TestCaseData([]).Returns(None, Seq.empty)
            yield TestCaseData(Seq.map id [ 1 ]).Returns(Some(1), Seq.empty)
            yield TestCaseData(Seq.map id [ 1; 2 ]).Returns(Some(1), [ 2 ])
        }


    [<TestCaseSource("decomposeTestCases")>]
    member this.testDecompose seq = Seq.decompose seq