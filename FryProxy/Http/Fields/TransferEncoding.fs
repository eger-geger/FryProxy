namespace FryProxy.Http.Fields

[<Struct>]
type TransferEncoding =
    { TransferEncoding: string list }

    interface IFieldModel<TransferEncoding> with
        static member Name = "Transfer-Encoding"

        member this.Encode() = this.TransferEncoding

        static member TryDecode(values: string list) =
            Some { TransferEncoding = values |> List.map (_.ToLowerInvariant()) }
