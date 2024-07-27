namespace FryProxy

open System
open System.IO
open System.Net.Security
open System.Security.Cryptography.X509Certificates
open System.Threading.Tasks
open FryProxy.Http
open FryProxy.Extension
open FryProxy.IO

/// Transmits encrypted HTTP traffic between client and server.
type Tunnel = delegate of handler: RequestHandlerChain * idleTimeout: TimeSpan -> Task

/// Creates a tunnel transmitting traffic between client and server.
type TunnelFactory = delegate of target: Target * client: ReadBuffer * server: ReadBuffer -> Tunnel ValueTask

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
    let transmit (cb: ReadBuffer) (sb: ReadBuffer) (timeout: TimeSpan) : Task =
        task {
            cb.Stream.ReadTimeout <- int timeout.TotalMilliseconds
            let! _ = Task.WhenAll(copy cb sb.Stream, copy sb cb.Stream)
            return ()
        }

    /// Create an opaque tunnel between client and server from read buffers.
    let establish clientBuffer serverBuffer =
        Tunnel(fun _ -> transmit clientBuffer serverBuffer) |> ValueTask.FromResult

    /// Opaque tunnel factory.
    let Factory = TunnelFactory(fun _ -> establish)

module TransparentTunnel =

    /// Explore HTTP traffic transmitted between client and server by passing it though chain of request handlers.
    /// Stop either when any of the connections is lost (with corresponding exception propagating to the caller) or
    /// it was explicitly closed via corresponding header field or client did not send anything for longer than timeout.
    let transmit
        (authOpt: SslServerAuthenticationOptions)
        authServerStream
        (clientBuff: ReadBuffer)
        (serverBuff: ReadBuffer)
        (chain: RequestHandlerChain)
        (timeout: TimeSpan)
        : Task =
        task {
            let connCloseRef = ref false
            use authServerStream = authServerStream
            use authClientStream = new SslStream(clientBuff.Stream, false)

            let authClientBuff = clientBuff.Share authClientStream
            let authServerBuff = serverBuff.Share authServerStream

            let handler =
                (fun _ -> ValueTask.FromResult(authServerBuff))
                |> Proxy.reverse
                |> chain.WrapOver(Handlers.connectionHeader connCloseRef).Seal

            do! authClientStream.AuthenticateAsServerAsync(authOpt)

            while not connCloseRef.Value do
                do! authClientStream.WaitInputAsync timeout
                do! Proxy.respond handler.Invoke authClientBuff
        }

    /// Establish a transparent tunnel between client and server acting as a middleman and authenticating to
    /// both using the provided options.
    let establish
        (clientOpt: SslClientAuthenticationOptions)
        (serverOpt: SslServerAuthenticationOptions)
        (target: Target)
        (clientBuff: ReadBuffer)
        (serverBuff: ReadBuffer)
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

        ValueTask.FromTask
        <| task {
            let sslServerStream = new SslStream(serverBuff.Stream, false)
            do! sslServerStream.AuthenticateAsClientAsync(targetOpt)
            return Tunnel(transmit serverOpt sslServerStream clientBuff serverBuff)
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
    let NaiveFactoryWithSelfSignedCertificate =
        NaiveFactoryWithServerCertificate X509Certificate.ProxyDefault

    /// <summary>A shorthand for <see cref="NaiveFactoryWithSelfSignedCertificate"/></summary>
    let DefaultFactory = NaiveFactoryWithSelfSignedCertificate
