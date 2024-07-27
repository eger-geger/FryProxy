namespace FryProxy.Http.Fields

[<Struct>]
type Connection =
    { Connection: string list }

    [<Literal>]
    static let CloseValue = "close"

    static member val Close = { Connection = [ CloseValue ] }
    member this.IsClose = List.contains CloseValue this.Connection

    interface IFieldModel<Connection> with
        static member val Name = "Connection"
        member this.Encode() = this.Connection
        static member TryDecode(values) = Some { Connection = values }
