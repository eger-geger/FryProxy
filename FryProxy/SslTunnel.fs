namespace FryProxy

open System
open System.IO
open System.Net.Security
open System.Security.Cryptography.X509Certificates
open System.Threading.Tasks
open FryProxy.Http
open FryProxy.Extension
open FryProxy.IO
open FryProxy.Pipeline

/// Transmits encrypted HTTP traffic between client and server over persistent connection(s).
/// Applies chain of request handlers to decrypted HTTP traffic if capable.
/// Terminates if client remains idle for longer then given inactivity timeout.
type 'T Tunnel = delegate of handler: 'T RequestHandlerChain * idleTimeout: TimeSpan -> Task

/// Creates long-lived TCP connections to destination, performing initial setup on newly established ones.
type TunnelConnectionFactory = delegate of (Stream -> Stream ValueTask) * Target -> IConnection ValueTask

/// Creates a tunnel for transmitting encrypted traffic between client and target.
type 'T TunnelFactory = delegate of TunnelConnectionFactory * Target * client: ReadBuffer -> 'T Tunnel ValueTask

/// Opaque tunnel blindly copies traffic between client and server in both directions without invoking handlers.
module OpaqueTunnel =

    /// Copy buffered stream content until end of stream is reached.
    let copy (src: ReadBuffer) dst =
        task {
            do! src.Copy (uint64 src.PendingSize) dst

            while true do
                try
                    let! n = src.Fill()
                    do! src.Copy (uint64 n) dst
                with :? EndOfStreamException ->
                    return ()
        }

    /// Blindly copy content between client and server in both directions until either some stream reached end
    /// or gets disconnected or client does not send anything for longer than timeout.
    let transmit (client: ReadBuffer) (server: IConnection) (timeout: TimeSpan) : Task =
        task {
            use srv = server
            client.Stream.ReadTimeout <- int timeout.TotalMilliseconds
            let! _ = Task.WhenAll(copy client srv.Buffer.Stream, copy srv.Buffer client.Stream)
            return ()
        }

    /// Create an opaque tunnel between client and server from read buffers.
    let establish (connector: TunnelConnectionFactory) target cb =
        ValueTask.FromTask
        <| task {
            let! conn = connector.Invoke(ValueTask.FromResult<_>, target)
            return Tunnel(fun _ -> transmit cb conn)
        }

    /// Opaque tunnel factory.
    let Factory = TunnelFactory(establish)

module TransparentTunnel =

    /// Explore HTTP traffic transmitted between client and server by passing it though chain of request handlers.
    /// Stop either when any of the connections is lost (with corresponding exception propagating to the caller) or
    /// it was explicitly closed via corresponding header field or client did not send anything for longer than timeout.
    let transmit
        (authOpt: SslServerAuthenticationOptions)
        (clientBuff: ReadBuffer)
        (connect: Target -> IConnection ValueTask)
        (chain: _ RequestHandlerChain)
        (timeout: TimeSpan)
        : Task =
        task {
            use sslClientStream = new SslStream(clientBuff.Stream, false)
            do! sslClientStream.AuthenticateAsServerAsync(authOpt)

            let clientBuff = clientBuff.Share sslClientStream

            let servePersistentConn () =
                task {
                    let! ctx = Handlers.proxyHttpMessage connect chain clientBuff
                    return ctx.KeepClientConnection
                }

            while! servePersistentConn() do
                do! sslClientStream.WaitInputAsync timeout
        }

    let authenticate (options: SslClientAuthenticationOptions) (stream: Stream) =
        ValueTask.FromTask
        <| task {
            let sslStream = new SslStream(stream, false)
            do! sslStream.AuthenticateAsClientAsync(options)

            return sslStream :> Stream
        }

    /// Establish a transparent tunnel between client and server acting as a middleman and authenticating to
    /// both using the provided options.
    let establish
        (clientOpt: SslClientAuthenticationOptions)
        (serverOpt: SslServerAuthenticationOptions)
        (connector: TunnelConnectionFactory)
        (target: Target)
        (clientBuff: ReadBuffer)
        =
        let targetOpt =
            SslClientAuthenticationOptions(
                TargetHost = target.Host,
                AllowTlsResume = clientOpt.AllowTlsResume,
                AllowRenegotiation = clientOpt.AllowRenegotiation,
                ClientCertificates = clientOpt.ClientCertificates,
                EnabledSslProtocols = clientOpt.EnabledSslProtocols,
                ApplicationProtocols = clientOpt.ApplicationProtocols,
                CertificateChainPolicy = clientOpt.CertificateChainPolicy,
                ClientCertificateContext = clientOpt.ClientCertificateContext,
                CertificateRevocationCheckMode = clientOpt.CertificateRevocationCheckMode,
                LocalCertificateSelectionCallback = clientOpt.LocalCertificateSelectionCallback,
                RemoteCertificateValidationCallback = clientOpt.RemoteCertificateValidationCallback
            )

        let connect target =
            connector.Invoke(authenticate targetOpt, target)

        ValueTask.FromTask
        <| task {
            use! conn = connect target // validate and pre-allocate connection
            do ignore conn
            return Tunnel(transmit serverOpt clientBuff connect)
        }

    /// Create the transparent tunnel factory using provided authentication options.
    let Factory clientAuthenticationOptions serverAuthenticationOptions =
        TunnelFactory(establish clientAuthenticationOptions serverAuthenticationOptions)

    /// Initializes factory with authentication options such that all remote certificates (client and server) are
    /// trusted and provided certificate is offered to the client when establishing a tunnel.
    let NaiveFactoryWithServerCertificate (certificate: X509Certificate) =
        let trustAll _ _ _ _ = true

        Factory
        <| SslClientAuthenticationOptions(RemoteCertificateValidationCallback = trustAll)
        <| SslServerAuthenticationOptions(
            ServerCertificate = certificate,
            RemoteCertificateValidationCallback = trustAll
        )

    /// <summary>
    /// Provides <see cref="NaiveFactoryWithServerCertificate"/> a temporary self-signed certificate.
    /// </summary>
    let NaiveFactoryWithSelfSignedCertificate () =
        Certificate.ProxyDefault() |> NaiveFactoryWithServerCertificate

    /// <summary>A shorthand for <see cref="NaiveFactoryWithSelfSignedCertificate"/></summary>
    let DefaultFactory () = NaiveFactoryWithSelfSignedCertificate()
