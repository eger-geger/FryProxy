namespace FryProxy.Tests.Http


type ReasonPhraseCodes() =

    static member supported =
        seq {
            yield! [ 100us; 101us ]
            yield! [ 200us .. 206us ]
            yield! List.add [ 300us .. 305us ] 307us
            yield! List.add [ 400us .. 417us ] 426us
            yield! [ 500us .. 505us ]
        }
