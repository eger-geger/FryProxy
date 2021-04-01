namespace FryProxy.Tests.Http

open FsUnit
open NUnit.Framework

open FryProxy.Http

type ReasonPhraseTests() =

    [<TestCaseSource(typeof<ReasonPhraseCodes>, nameof ReasonPhraseCodes.supported)>]
    member this.testFromStatusCode code =
        ReasonPhrase.forStatusCode code
        |> should be (not' NullOrEmptyString)
