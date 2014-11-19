using System;
using System.Diagnostics.Contracts;
using System.Net.Security;
using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;

using FryProxy.Headers;
using FryProxy.Utility;

namespace FryProxy {

    /// <summary>
    ///     HTTP proxy capable to intercept HTTPS requests.
    ///     Authenticates to client and server using provided <see cref="X509Certificate" />
    /// </summary>
    public class SslProxy : HttpProxy {

        private const Int32 DefaultSecureHttpPort = 443;

        private readonly X509Certificate _certificate;

        private readonly RemoteCertificateValidationCallback _certificateValidationCallback;

        private static readonly RemoteCertificateValidationCallback DefaultCertificateValidationCallback = (sender, certificate, chain, errors) => true;

        /// <summary>
        ///     Creates new instance of <see cref="HttpProxy" /> using provided default port and internal buffer size.
        /// </summary>
        /// <param name="defaultPort">
        ///     Port number on destination server which will be used if not specified in request
        /// </param>
        /// <param name="bufferSize">
        ///     Size of buffer used internally for copying streams
        /// </param>
        /// <param name="certificate">
        ///     Certificate used for server authentication
        /// </param>
        /// <param name="certificateValidationCallback">
        ///     Used to validate destination server certificate. By default it accepts anything provided by server
        /// </param>
        public SslProxy(X509Certificate certificate, Int32 defaultPort, Int32 bufferSize, RemoteCertificateValidationCallback certificateValidationCallback = null)
            : base(defaultPort, bufferSize) {
            Contract.Requires<ArgumentNullException>(certificate != null, "certificate");

            _certificateValidationCallback = certificateValidationCallback ?? DefaultCertificateValidationCallback;

            _certificate = certificate;
        }

        /// <summary>
        ///     Creates new instance of <see cref="HttpProxy" /> using provided default port.
        /// </summary>
        /// <param name="defaultPort">
        ///     Port number on destination server which will be used if not specified in request
        /// </param>
        /// <param name="certificate">
        ///     Certificate used for server authentication
        /// </param>
        /// <param name="certificateValidationCallback">
        ///     Used to validate destination server certificate. By default it accepts anything provided by server
        /// </param>
        public SslProxy(X509Certificate certificate, Int32 defaultPort, RemoteCertificateValidationCallback certificateValidationCallback = null)
            : this(certificate, defaultPort, DefaultBufferSize, certificateValidationCallback) { }

        /// <summary>
        ///     Creates new instance of <see cref="HttpProxy" /> using default HTTP port (443).
        /// </summary>
        /// <param name="certificate">
        ///     Certificate used for server authentication
        /// </param>
        /// <param name="certificateValidationCallback">
        ///     Used to validate destination server certificate. By default it accepts anything provided by server
        /// </param>
        public SslProxy(X509Certificate certificate, RemoteCertificateValidationCallback certificateValidationCallback = null) 
            : this(certificate, DefaultSecureHttpPort, certificateValidationCallback) { }

        /// <summary>
        ///     Establish secured connection to destination server.
        /// </summary>
        /// <param name="context">current request context</param>
        protected override void ConnectToServer(ProcessingContext context) {
            base.ConnectToServer(context);

            if (context.ServerStream == null) {
                throw new InvalidContextException("ServerStream");
            }

            if (context.ServerEndPoint == null) {
                throw new InvalidContextException("ServerEndPoint");
            }

            var sslServerStream = new SslStream(context.ServerStream, false, _certificateValidationCallback);

            sslServerStream.AuthenticateAsClient(context.ServerEndPoint.Host);

            context.ServerStream = sslServerStream;

            Logger.DebugFormat("Authenticated as [{0}] client", context.ServerEndPoint.Host);
        }

        /// <summary>
        ///     Establish secured connection with client and receive HTTP request using it.
        /// </summary>
        /// <param name="context">current request context</param>
        protected override void ReceiveRequest(ProcessingContext context) {
            base.ReceiveRequest(context);

            if (context.RequestHeaders == null) {
                throw new InvalidOperationException("Not SSL request");
            }

            if (context.RequestHeaders.MethodType != RequestMethodTypes.CONNECT) {
                throw new InvalidContextException("RequestHeaders");
            }

            if (context.ClientStream == null) {
                throw new InvalidContextException("ClientStream");
            }

            context.ClientStream.SendConnectionEstablished();

            var sslClientStream = new SslStream(context.ClientStream, false);

            sslClientStream.AuthenticateAsServer(_certificate, false, SslProtocols.Tls, false);

            context.ClientStream = sslClientStream;

            base.ReceiveRequest(context);
        }

    }

}