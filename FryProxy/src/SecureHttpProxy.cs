using System;
using System.IO;
using System.Net;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;

namespace FryProxy {

    public class SecureHttpProxy : HttpProxy {

        private const Int32 DefaultSecureHttpPort = 443;

        private readonly X509Certificate _certificate;

        public SecureHttpProxy(X509Certificate certificate, Int32 defaultPort) : base(defaultPort) {
            _certificate = certificate;
        }

        public SecureHttpProxy(X509Certificate certificate) : this(certificate, DefaultSecureHttpPort) {}

        protected sealed override Stream CreateClientStream(Socket clientSocket) {
            var stream = new SslStream(base.CreateClientStream(clientSocket));

            stream.AuthenticateAsServer(_certificate, false, SslProtocols.Default, false);

            Logger.DebugFormat("Client certificate: {0}", stream.RemoteCertificate);

            if (!stream.IsAuthenticated) {
                throw new InvalidOperationException("Client authentification failed");
            }

            return stream;
        }

        protected sealed override Stream CreateServerStream(DnsEndPoint requestEndPoint) {
            var stream = new SslStream(base.CreateServerStream(requestEndPoint));

            stream.AuthenticateAsClient(requestEndPoint.Host);

            Logger.DebugFormat("Server certificate: {0}", stream.RemoteCertificate);

            if (!stream.IsAuthenticated) {
                throw new InvalidOperationException("Server authentification failed");
            }

            return stream;
        }

    }

}