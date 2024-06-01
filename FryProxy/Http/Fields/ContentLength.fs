namespace FryProxy.Http.Fields

open System

[<Struct>]
type ContentLength =
    { ContentLength: uint64 }

    interface IFieldModel<ContentLength> with
        static member val Name = "Content-Length"

        member this.Encode() = [ this.ContentLength.ToString() ]

        static member TryDecode(values: string list) =
            values
            |> List.tryExactlyOne
            |> Option.bind (UInt64.TryParse >> Option.ofAttempt)
            |> Option.map (fun length -> { ContentLength = length })
