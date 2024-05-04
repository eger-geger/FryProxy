namespace FryProxy.Tests

open NUnit.Framework

type OptionTests() =

    static member private attemptTestCases =
        seq {
            yield TestCaseData(true, 44).Returns(Some 44)
            yield TestCaseData(false, 44).Returns(None)
        }

    [<TestCaseSource(nameof OptionTests.attemptTestCases)>]
    member this.ofAttemptTest(success, value) = Option.ofAttempt (success, value)
