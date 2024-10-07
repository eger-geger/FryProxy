namespace FryProxy.Http.Fields

open System
open FryProxy.Http
open FryProxy.Http.Protocol

[<Struct>]
type Connection =
    { Connection: string list }

    [<Literal>]
    static let CloseToken = "close"

    [<Literal>]
    static let KeepAliveToken = "keep-alive"

    static member Close = { Connection = [ CloseToken ] }

    static member KeepAlive = { Connection = [ KeepAliveToken ] }

    static member CloseField: Field = FieldOf Connection.Close

    static member KeepAliveField: Field = FieldOf Connection.KeepAlive

    member this.IsClose = List.contains CloseToken this.Connection

    member this.IsKeepAlive = List.contains KeepAliveToken this.Connection

    interface IFieldModel<Connection> with
        static member Name = "Connection"
        member this.Encode() = this.Connection
        static member TryDecode(values) = Some { Connection = values }

module Connection =

    /// Determine weather connection over which a message was received can be reused based on
    /// message HTTP version and value (or absence) of the Connection header field.
    let isReusable (ver: Version) (conn: Connection Option) =
        match conn with
        | Some(conn: Connection) -> (ver = Http11 && not conn.IsClose) || (ver = Http10 && conn.IsKeepAlive)
        | None -> ver = Http11

    /// Choose appropriate Connection header field for a message given a protocol.
    let makeField (httpVer: Version) (keepAlive: bool) =
        if not keepAlive && httpVer = Http11 then
            ValueSome Connection.CloseField
        elif keepAlive && httpVer = Http10 then
            ValueSome Connection.KeepAliveField
        else
            ValueNone
