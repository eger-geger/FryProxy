namespace FryProxy.Http.Fields

open System.Text

[<Struct>]
type ContentType =
    { ContentType: string list }

    static member TextPlain(enc: Encoding) =
        { ContentType = [ $"text/plain; encoding={enc.BodyName}" ] }

    static member MessageHttp = { ContentType = [ "message/http" ] }

    interface IFieldModel<ContentType> with
        static member val Name = "Content-Type"

        member this.Encode() = this.ContentType

        static member TryDecode(values: string list) = Some { ContentType = values }
