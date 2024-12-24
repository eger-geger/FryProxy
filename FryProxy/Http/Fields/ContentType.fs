namespace FryProxy.Http.Fields

open System.Text

[<Struct>]
type ContentType =
    { ContentType: string }

    static member TextPlain(enc: Encoding) =
        { ContentType = $"text/plain; encoding={enc.BodyName}" }

    static member MessageHttp = { ContentType = "message/http" }

    interface IFieldModel<ContentType> with
        static member Name = "Content-Type"

        member this.Encode() = this.ContentType

        static member TryDecode value = Some { ContentType = value }
