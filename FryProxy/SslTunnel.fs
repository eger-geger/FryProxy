namespace FryProxy

open System.IO
open System.Net.Security
open System.Security.Cryptography.X509Certificates
open System.Threading.Tasks
open FryProxy.Http
open FryProxy.Extension


/// Transmits encrypted HTTP traffic between client and server.
type ITunnel =

    /// Establish tunneled connection to a remote server.
    abstract member EstablishAsync: Target * Context -> Stream Task

    /// Relay encrypted HTTP request and response between client and server
    /// using previously established connection.
    abstract member RelayAsync: Stream * Stream * Context -> Task


/// Transmits unencrypted traffic.
type OpaqueTunnel() =
    let copy (ctx: Context) src dst =
        task {
            let buff = ctx.AllocateBuffer(src)

            while true do
                try
                    let! n = buff.Fill()
                    do! buff.Copy (uint64 n) dst
                with :? EndOfStreamException ->
                    return ()
        }

    interface ITunnel with
        override _.EstablishAsync(target, context) =
            task {
                let! stream = context.ConnectAsync(target)
                return stream :> Stream
            }

        override _.RelayAsync(client, server, ctx) =
            let cp = copy ctx
            let upstream = cp client server
            let downstream = cp server client

            task {
                do! upstream
                do! downstream
            }

/// Decrypts transmitted traffic, transforms it via request handler chain and encrypts it back.
type TransparentTunnel(serverOptions: SslServerAuthenticationOptions, clientOptions: SslClientAuthenticationOptions) =

    let withClientOpts host =
        SslClientAuthenticationOptions(
            TargetHost = host,
            AllowTlsResume = clientOptions.AllowTlsResume,
            AllowRenegotiation = clientOptions.AllowRenegotiation,
            ClientCertificates = clientOptions.ClientCertificates,
            EnabledSslProtocols = clientOptions.EnabledSslProtocols,
            ApplicationProtocols = clientOptions.ApplicationProtocols,
            CertificateChainPolicy = clientOptions.CertificateChainPolicy,
            ClientCertificateContext = clientOptions.ClientCertificateContext,
            CertificateRevocationCheckMode = clientOptions.CertificateRevocationCheckMode,
            LocalCertificateSelectionCallback = clientOptions.LocalCertificateSelectionCallback,
            RemoteCertificateValidationCallback = clientOptions.RemoteCertificateValidationCallback
        )

    new(certificate: X509Certificate) =
        let trustAll _ _ _ _ = true

        TransparentTunnel(
            SslServerAuthenticationOptions(
                ServerCertificate = certificate,
                RemoteCertificateValidationCallback = trustAll
            ),
            SslClientAuthenticationOptions(RemoteCertificateValidationCallback = trustAll)
        )

    new() = TransparentTunnel(X509Certificate.ProxyDefault)

    interface ITunnel with
        override _.EstablishAsync(target, context) =
            task {
                let! nwStream = context.ConnectAsync(target)
                let sslStream = new SslStream(nwStream, true)
                do! sslStream.AuthenticateAsClientAsync(withClientOpts target.Host)
                return sslStream :> Stream
            }

        override _.RelayAsync(client, server, context) =
            let chain = context.CompleteChain(fun _ -> Task.FromResult server)

            task {
                use sslClient = new SslStream(client, true)
                do! sslClient.AuthenticateAsServerAsync(serverOptions)
                do! context.AllocateBuffer(sslClient) |> Proxy.respond chain.Invoke
            }
