namespace FryProxy.Http.Fields

open FryProxy.Http

[<Struct>]
type Connection =
    { Connection: string list }

    [<Literal>]
    static let CloseValue = "close"

    static member val Close = { Connection = [ CloseValue ] }

    static member CloseField: Field = Connection.Close.ToField()

    member this.IsClose = List.contains CloseValue this.Connection

    interface IFieldModel<Connection> with
        static member val Name = "Connection"
        member this.Encode() = this.Connection
        static member TryDecode(values) = Some { Connection = values }
