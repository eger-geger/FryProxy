namespace FryProxy.Tests

open NUnit.Framework

type OptionTests() =

    static member private traverseTestCases =
        seq {
            yield TestCaseData(Seq.empty<obj option>).Returns(Some List.empty)

            yield
                TestCaseData(Seq.map Some [ 1 .. 5 ])
                    .Returns(Some [ 1 .. 5 ])

            yield
                TestCaseData([ Some(1); None; Some(3) ])
                    .Returns(None)
        }

    [<TestCaseSource(nameof OptionTests.traverseTestCases)>]
    member this.traverseTest seq = Option.traverse seq
    
    static member private attemptTestCases =
        seq {
            yield TestCaseData(true, 44).Returns(Some 44)
            yield TestCaseData(false, 44).Returns(None)
        }
    
    [<TestCaseSource(nameof OptionTests.attemptTestCases)>]
    member this.ofAttemptTest (success, value) = Option.ofAttempt(success, value)
