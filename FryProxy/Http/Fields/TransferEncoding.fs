namespace FryProxy.Http.Fields

open FryProxy.Http

[<Struct>]
type TransferEncoding =
    { TransferEncoding: string list }

    interface IFieldModel<TransferEncoding> with
        static member Name = "Transfer-Encoding"

        member this.Encode() = Field.joinValues this.TransferEncoding

        static member TryDecode(values: string) =
            Some { TransferEncoding = values |> Field.splitValues |> List.map(_.ToLowerInvariant()) }
