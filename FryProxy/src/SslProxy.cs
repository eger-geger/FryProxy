using System;
using System.Diagnostics.Contracts;
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

        protected override void ConnectToServer(ProcessingContext context) {
            base.ConnectToServer(context);

            if (context.ServerStream == null) {
                throw new InvalidOperationException("context#ServerStream should not be null");
            }

            if (context.ServerEndPoint == null) {
                throw new InvalidOperationException("context#ServerEndPoint should not be null");
            }

            var sslServerStream = new SslStream(context.ServerStream, false);

            sslServerStream.AuthenticateAsClient(context.ServerEndPoint.Host);

            context.ServerStream = sslServerStream;

            Logger.InfoFormat("Authenticated as [{0}] client", context.ServerEndPoint.Host);
        }

        protected override void ReceiveRequest(ProcessingContext context) {
            base.ReceiveRequest(context);

            if (context.ClientStream == null) {
                throw new InvalidOperationException("context#ClientStream should not be null");
            }

            if (context.RequestHeaders == null) {
                throw new InvalidOperationException("context#RequestHeaders should not be null");
            }

            if (!context.RequestHeaders.IsRequestMethod(RequestMethods.CONNECT)) {
                Logger.Warn("Abandon processing non-ssl request");
                return;
            }

            context.ClientStream.SendConnectionEstablished();

            var sslClientStream = new SslStream(context.ClientStream, false);

            sslClientStream.AuthenticateAsServer(_certificate, false, SslProtocols.Tls, false);

            context.ClientStream = sslClientStream;

            base.ReceiveRequest(context);
        }

    }

}