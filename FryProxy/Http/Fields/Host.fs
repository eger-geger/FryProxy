namespace FryProxy.Http.Fields

[<Struct>]
type Host =
    { Host: string }

    interface IFieldModel<Host> with
        static member Name = "Host"

        member this.Encode() = this.Host

        static member TryDecode value = Some { Host = value }
