namespace FryProxy.Http.Fields

open System
open FryProxy.Extension

[<Struct>]
type MaxForwards =
    { MaxForwards: uint }

    interface IFieldModel<MaxForwards> with

        static member Name = "MaxForwards"
        
        member this.Encode() = [ this.MaxForwards.ToString() ]

        static member TryDecode values =
            values
            |> List.tryExactlyOne
            |> Option.bind(UInt32.TryParse >> Option.ofAttempt)
            |> Option.map(fun v -> { MaxForwards = v })
