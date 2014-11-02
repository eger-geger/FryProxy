using System;
using System.Diagnostics.Contracts;
using System.IO;
using System.Net;
using System.Net.Security;
using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;

using FryProxy.Headers;
using FryProxy.Utility;

namespace FryProxy {

    public class SslProxy : HttpProxy {

        private const Int32 DefaultSecureHttpPort = 443;

        private readonly X509Certificate _certificate;

        public SslProxy(X509Certificate certificate, Int32 defaultPort) : base(defaultPort) {
            Contract.Requires<ArgumentNullException>(certificate != null, "certificate");
            _certificate = certificate;
        }

        public SslProxy(X509Certificate certificate) : this(certificate, DefaultSecureHttpPort) {}

        protected override sealed void HandleRequestReceived(HttpRequestHeaders headers, Stream clientStream, ref Boolean stopProcessing) {
            Contract.Requires<ArgumentNullException>(headers != null, "headers");
            Contract.Requires<ArgumentNullException>(clientStream != null, "clientStream");

            if (!headers.IsRequestMethod(RequestMethods.CONNECT)) {
                return;
            }

            stopProcessing = true;

            clientStream.SendConnectionEstablished();

            var sslClientStream = new SslStream(clientStream, false);

            try {
                sslClientStream.AuthenticateAsServer(_certificate, false, SslProtocols.Tls, false);
            } catch (Exception ex) {
                Logger.Error("Failed to authenticate as server", ex);
                throw;
            }

            RelayHttpMessage(sslClientStream);
        }

        protected override Stream CreateServerStream(DnsEndPoint requestEndPoint) {
            var sslServerStream = new SslStream(base.CreateServerStream(requestEndPoint));

            try {
                sslServerStream.AuthenticateAsClient(requestEndPoint.Host);
            } catch (Exception ex) {
                Logger.Error("Failed to authenticate as client", ex);
                throw;
            }

            return sslServerStream;
        }

    }

}