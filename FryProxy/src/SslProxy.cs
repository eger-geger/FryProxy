using System;
using System.Diagnostics.Contracts;
using System.IO;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;
using FryProxy.Headers;
using FryProxy.Utils;
using FryProxy.Writers;

namespace FryProxy
{
    /// <summary>
    ///     HTTP proxy capable to intercept HTTPS requests.
    ///     Authenticates to client and server using provided <see cref="X509Certificate" />
    /// </summary>
    public class SslProxy : HttpProxy
    {
        private const Int32 DefaultSecureHttpPort = 443;

        private static readonly RemoteCertificateValidationCallback DefaultCertificateValidationCallback =
            (sender, certificate, chain, errors) => true;

        private readonly X509Certificate _certificate;

        private readonly RemoteCertificateValidationCallback _certificateValidationCallback;

        /// <summary>
        ///     Creates new instance of <see cref="HttpProxy" /> using provided default port and internal buffer size.
        /// </summary>
        /// <param name="defaultPort">
        ///     Port number on destination server which will be used if not specified in request
        /// </param>
        /// <param name="certificate">
        ///     Certificate used for server authentication
        /// </param>
        /// <param name="rcValidationCallback">
        ///     Used to validate destination server certificate. By default it accepts anything provided by server
        /// </param>
        public SslProxy(X509Certificate certificate, Int32 defaultPort,
            RemoteCertificateValidationCallback rcValidationCallback = null) : base(defaultPort)
        {
            Contract.Requires<ArgumentNullException>(certificate != null, "certificate");

            _certificateValidationCallback = rcValidationCallback ?? DefaultCertificateValidationCallback;

            _certificate = certificate;
        }

        /// <summary>
        ///     Creates new instance of <see cref="HttpProxy" /> using default HTTP port (443).
        /// </summary>
        /// <param name="certificate">
        ///     Certificate used for server authentication
        /// </param>
        /// <param name="certificateValidationCallback">
        ///     Used to validate destination server certificate. By default it accepts anything provided by server
        /// </param>
        public SslProxy(X509Certificate certificate,
            RemoteCertificateValidationCallback certificateValidationCallback = null)
            : this(certificate, DefaultSecureHttpPort, certificateValidationCallback)
        {
        }

        /// <summary>
        ///     Establish secured connection to destination server.
        /// </summary>
        /// <param name="context">current request context</param>
        protected override void ConnectToServer(ProcessingContext context)
        {
            base.ConnectToServer(context);

            if (context.ServerStream == null)
            {
                throw new InvalidContextException("ServerStream");
            }

            if (context.ServerEndPoint == null)
            {
                throw new InvalidContextException("ServerEndPoint");
            }

            var sslServerStream = new SslStream(context.ServerStream, false, _certificateValidationCallback);
            sslServerStream.AuthenticateAsClient(context.ServerEndPoint.Host);
            context.ServerStream = sslServerStream;

            if (Logger.IsDebugEnabled)
            {
                Logger.DebugFormat("SSL Connection Established: {0}:{1}",
                    context.ServerEndPoint.Host,
                    context.ServerEndPoint.Port
                    );
            }
        }

        /// <summary>
        ///     Establish secured connection with client and receive HTTP request using it.
        /// </summary>
        /// <param name="context">current request context</param>
        protected override void ReceiveRequest(ProcessingContext context)
        {
            base.ReceiveRequest(context);

            if (context.RequestHeader == null)
            {
                throw new InvalidOperationException("Not SSL request");
            }

            if (context.RequestHeader.MethodType != RequestMethodTypes.CONNECT)
            {
                throw new InvalidContextException("RequestHeader");
            }

            if (context.ClientStream == null)
            {
                throw new InvalidContextException("ClientStream");
            }

            var responseWriter = new HttpResponseWriter(context.ClientStream);

            var sslStream = new SslStream(context.ClientStream, false, _certificateValidationCallback);

            try
            {
                responseWriter.WriteConnectionEstablished();

                sslStream.AuthenticateAsServer(_certificate, false, SslProtocols.Tls, false);

                context.ClientStream = sslStream;

                if (Logger.IsDebugEnabled)
                {
                    Logger.Debug("Client SSL connection established");
                }

                base.ReceiveRequest(context);
            }
            catch (IOException ex)
            {
                context.StopProcessing();

                if (ex.IsSocketException(SocketError.ConnectionReset, SocketError.ConnectionAborted))
                {
                    if (Logger.IsDebugEnabled)
                    {
                        Logger.Debug("Request aborted");
                    }
                }
                else
                {
                    Logger.Error("Failed to read request", ex);
                    Logger.Error(context.RequestHeader);

                    throw;
                }
            }
            
        }
    }
}