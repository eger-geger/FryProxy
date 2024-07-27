namespace FryProxy

open System
open System.Net

/// Socket read and write timeouts.
type SocketTimeouts() =

    /// Socket read timeout.
    member val Read = TimeSpan.FromSeconds(-1) with get, set

    /// Socket write timeout.
    member val Write = TimeSpan.FromSeconds(-1) with get, set

    /// 30 seconds for reading and writing.
    static member Default =
        let ts = TimeSpan.FromSeconds(30)
        SocketTimeouts(Read = ts, Write = ts)

[<AutoOpen>]
module SocketExtensions =
    open System.Net.Sockets
    
    type Socket with

        /// Synonym for receive and send timeouts.
        member this.Timeouts
            with get () =
                SocketTimeouts(
                    Read = TimeSpan.FromMilliseconds(this.ReceiveTimeout),
                    Write = TimeSpan.FromMilliseconds(this.SendTimeout)
                )
            and set (timeouts: SocketTimeouts) =
                this.SendTimeout <- int timeouts.Write.TotalMilliseconds
                this.ReceiveTimeout <- int timeouts.Read.TotalMilliseconds

        /// Set send and receive buffers to the same size.
        member this.BufferSize
            with set (size: int) =
                this.ReceiveBufferSize <- size
                this.SendBufferSize <- size

type Settings() =

    /// The port on which proxy should listen for incoming HTTP requests.
    /// Zero (default) means pick any available port.
    member val Port = 0u with get, set

    /// Address on which proxy will listen for incoming HTTP requests.
    /// Listen on all addresses by default.
    member val Address = IPAddress.Any with get, set

    /// Upper bound on number of concurrent HTTP requests proxy would accept.
    member val BacklogSize = 0xffffu with get, set

    /// Size of the buffer allocated for reading HTTP request and response.
    /// Determines maximum allowed HTTP header size.
    member val BufferSize = 0x2000 with get, set

    /// Destination port to use when request does not explicitly define one.
    member val DefaultRequestPort = 80 with get, set

    /// Inbound (from client to proxy) socket timeouts.
    member val ClientTimeouts = SocketTimeouts.Default with get, set

    /// Outbound (from proxy to request target) socket timeouts.
    member val UpstreamTimeouts = SocketTimeouts.Default with get, set
    
    /// How long before dropping persistent idle client connection.
    member val ClientIdleTimeout = TimeSpan.FromMinutes(1)