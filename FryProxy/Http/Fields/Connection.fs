namespace FryProxy.Http.Fields

open FryProxy.Http

[<Struct>]
type Connection =
    { Connection: string list }

    [<Literal>]
    static let CloseToken = "close"

    [<Literal>]
    static let KeepAliveToken = "keep-alive"

    static member CloseField: Field = FieldOf { Connection = [ CloseToken ] }

    static member KeepAliveField: Field = FieldOf { Connection = [ KeepAliveToken ] }

    member this.Close = List.contains CloseToken this.Connection

    member this.KeepAlive = List.contains KeepAliveToken this.Connection

    interface IFieldModel<Connection> with
        static member val Name = "Connection"
        member this.Encode() = this.Connection
        static member TryDecode(values) = Some { Connection = values }
