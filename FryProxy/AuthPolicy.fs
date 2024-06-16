namespace FryProxy

open System.IO
open System.Net.Security
open System.Security.Authentication
open System.Security.Cryptography.X509Certificates
open System.Threading.Tasks
open FryProxy.Extension

/// Authenticates proxy to clients and upstream servers.
type IAuthPolicy =

    /// Authenticates proxy inbound client connection returning plain HTTP stream.
    abstract member AuthClient: Stream -> Stream ValueTask

    /// Authenticates proxy outbound connection returning plain HTTP stream.
    abstract member AuthServer: host: string * Stream -> Stream ValueTask

/// Does not perform any authentication leaving the stream unchanged.
type Unauthenticated() =
    interface IAuthPolicy with
        member _.AuthClient s = ValueTask.FromResult(s)
        member _.AuthServer(_, s) = ValueTask.FromResult(s)

/// Offers clients an SSL certificate and accepts any upstream server certificate.
type SslAuthentication(certificate: X509Certificate, protocols: SslProtocols) =
    let serverOptions =
        SslServerAuthenticationOptions(ServerCertificate = certificate, EnabledSslProtocols = protocols)

    let clientOptions host =
        SslClientAuthenticationOptions(
            TargetHost = host,
            EnabledSslProtocols = protocols,
            RemoteCertificateValidationCallback = fun _ _ _ _ -> true
        )

    new(certificate: X509Certificate) = SslAuthentication(certificate, SslProtocols.None)

    interface IAuthPolicy with
        member _.AuthClient stream =
            ValueTask.FromTask
            <| task {
                let ss = new SslStream(stream)
                do! ss.AuthenticateAsServerAsync(serverOptions)
                return ss :> Stream
            }

        member _.AuthServer(host, stream) =
            ValueTask.FromTask
            <| task {
                let ss = new SslStream(stream)
                do! ss.AuthenticateAsClientAsync(clientOptions host)
                return ss :> Stream
            }
