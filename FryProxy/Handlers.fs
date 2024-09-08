module FryProxy.Handlers

open System
open System.Net.Http
open System.Threading.Tasks
open FryProxy.Http
open FryProxy.Extension
open FryProxy.Http.Fields
open FryProxy.IO

/// Handles CONNECT requests by establishing a tunnel using provided factory.
let tunnel factory (result: _ ref) req (next: RequestHandler) =
    if req.Header.StartLine.Method = HttpMethod.Connect then
        ValueTask.FromTask
        <| task {
            let! resp, cxnOpt = Proxy.tunnel factory req.Header

            match cxnOpt with
            | ValueSome v -> result.Value <- v
            | ValueNone -> result.Value <- null

            return resp
        }
    else
        next.Invoke req

/// Drops 'Connection' request field and report whether it requested termination.
let requestConnectionHeader (close: bool ref) req (next: RequestHandler) =
    match Connection.TryPop req.Header.Fields with
    | Some(conn: Connection), requestFields ->
        close.Value <- conn.IsClose

        ValueTask.FromTask
        <| task {
            let! resp = next.Invoke({ req with Header.Fields = requestFields })

            let responseFields' =
                if conn.IsClose then
                    let (_: Connection Option, fields') = Connection.TryPop resp.Header.Fields
                    Connection.Close.ToField() :: fields'
                else
                    resp.Header.Fields

            return { resp with Header.Fields = responseFields' }
        }
    | _ ->
        close.Value <- false
        next.Invoke req

/// Drops 'Connection' response field and report whether it requested termination.
let responseConnectionHeader (close: bool ref) req (next: RequestHandler) =
    ValueTask.FromTask
    <| task {
        let! resp = next.Invoke req

        match Connection.TryPop resp.Header.Fields with
        | Some(conn: Connection), fields' ->
            close.Value <- conn.IsClose
            return { resp with Header.Fields = fields' }
        | _ ->
            close.Value <- false
            return resp
    }

/// Serve incoming proxy request by repeating it to original target and sending the response back.
/// Returns flag indicating whether client connection can remain persistent.
let serveRequest (connect: Target -> IConnection ValueTask) (chain: RequestHandlerChain) clientBuff =
    task {
        let closeClientConn = ref false
        let closeServerConn = ref false
        let serverConn = ref Connection.Empty

        use requestScope =
            { new IDisposable with
                member _.Dispose() =
                    use conn = serverConn.Value

                    if closeServerConn.Value then
                        conn.Close() }

        do ignore requestScope

        let establishScopedConnection t =
            ValueTask.FromTask
            <| task {
                let! conn = connect t
                serverConn.Value <- conn
                return conn.Buffer
            }

        let handler =
            requestConnectionHeader closeClientConn
            +> responseConnectionHeader closeServerConn
            +> chain
            |> _.Seal(Proxy.reverse establishScopedConnection)


        do! Proxy.respond handler.Invoke clientBuff

        return not closeClientConn.Value
    }
