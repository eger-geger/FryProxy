namespace FryProxy.Http

open System
open System.Text


[<Struct>]
type Host =
    { Host: string }

    interface IFieldModel<Host> with
        static member val Name = "Host"

        member this.Encode() = [ this.Host ]

        static member TryDecode values =
            List.tryExactlyOne values |> Option.map (fun host -> { Host = host })

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

[<Struct>]
type TransferEncoding =
    { TransferEncoding: string list }

    interface IFieldModel<TransferEncoding> with
        static member val Name = "Transfer-Encoding"

        member this.Encode() = this.TransferEncoding

        static member TryDecode(values: string list) =
            Some { TransferEncoding = values |> List.map (_.ToLowerInvariant()) }


[<Struct>]
type ContentType =
    { ContentType: string list }

    static member TextPlain(enc: Encoding) =
        { ContentType = [ $"text/plain; encoding={enc.BodyName}" ] }

    interface IFieldModel<ContentType> with
        static member val Name = "Content-Type"

        member this.Encode() = this.ContentType

        static member TryDecode(values: string list) = Some { ContentType = values }
