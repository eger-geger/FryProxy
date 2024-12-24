namespace FryProxy.Http.Fields

open System

type Expect =
    { Expect: string }

    [<Literal>]
    static let ContinueToken = "100-continue"

    member this.IsContinue = this.Expect = ContinueToken

    interface Expect IFieldModel with
        static member Name = "Expect"

        member this.Encode() = this.Expect

        static member TryDecode value = Some { Expect = value }
