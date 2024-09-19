namespace FryProxy.Http.Fields

open FryProxy.Http

[<Struct>]
type Connection =
    { Connection: string list }

    [<Literal>]
    static let CloseValue = "close"

    [<Literal>]
    static let KeepAliveValue = "keep-alive"

    static member val Close = { Connection = [ CloseValue ] }

    static member val KeepAlive = { Connection = [ KeepAliveValue ] }

    static member CloseField: Field = FieldOf Connection.Close

    static member KeepAliveField: Field = FieldOf Connection.KeepAlive

    member this.ShouldClose = List.contains CloseValue this.Connection

    member this.ShouldKeep = List.contains KeepAliveValue this.Connection

    interface IFieldModel<Connection> with
        static member val Name = "Connection"
        member this.Encode() = this.Connection
        static member TryDecode(values) = Some { Connection = values }
