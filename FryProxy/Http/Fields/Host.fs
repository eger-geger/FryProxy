namespace FryProxy.Http.Fields

[<Struct>]
type Host =
    { Host: string }

    interface IFieldModel<Host> with
        static member Name = "Host"

        member this.Encode() = [ this.Host ]

        static member TryDecode values =
            List.tryExactlyOne values |> Option.map (fun host -> { Host = host })
