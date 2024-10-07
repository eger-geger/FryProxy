namespace FryProxy.Http.Fields

open System

type Expect =
    { Expect: string list }

    [<Literal>]
    static let ContinueToken = "100-continue"

    member this.IsContinue =
        let inline isContinue v =
            String.Equals(v, ContinueToken, StringComparison.InvariantCultureIgnoreCase)

        this.Expect
        |> List.tryExactlyOne
        |> Option.map isContinue
        |> Option.defaultValue false

    interface Expect IFieldModel with
        static member Name = "Expect"

        member this.Encode() = this.Expect

        static member TryDecode vals =
            if vals.IsEmpty then
                None
            else
                Some { Expect = vals }
