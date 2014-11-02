using System;
using System.Diagnostics.Contracts;
using System.IO;
using System.Net;
using System.Net.Sockets;

using FryProxy.Handlers;
using FryProxy.Headers;
using FryProxy.Utility;

using log4net;

namespace FryProxy {

    public class HttpProxy {

        public delegate void RequestHandler(ProcessingContext processingContext);

        private const Int32 DefaultHttpPort = 80;

        protected readonly ILog Logger;

        private readonly Int32 _defaultPort;

        public RequestHandler OnRequestReceived, OnResponseReceived, OnServerConnected;

        public HttpProxy() : this(DefaultHttpPort) {}

        public HttpProxy(Int32 defaultPort) {
            Contract.Requires<ArgumentOutOfRangeException>(defaultPort > IPEndPoint.MinPort && defaultPort < IPEndPoint.MaxPort, "defaultPort");

            _defaultPort = defaultPort;

            Logger = LogManager.GetLogger(GetType());

            ClientTimeout = TimeSpan.FromSeconds(2);
            ServerTimeout = TimeSpan.FromSeconds(2);
        }

        public Int32 BufferSize {
            get { return 4096; }
        }

        public TimeSpan ClientTimeout { get; set; }

        public TimeSpan ServerTimeout { get; set; }

        private Stream CreateClientStream(Socket clientSocket) {
            Contract.Requires<ArgumentNullException>(clientSocket != null, "clientSocket");

            clientSocket.ReceiveTimeout = (Int32) ClientTimeout.TotalMilliseconds;
            clientSocket.SendTimeout = (Int32) ClientTimeout.TotalMilliseconds;

            return new NetworkStream(clientSocket, true);
        }

        protected virtual Stream CreateServerStream(DnsEndPoint requestEndPoint) {
            Contract.Requires<ArgumentNullException>(requestEndPoint != null, "requestEndPoint");

            var socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp) {
                ReceiveTimeout = (Int32) ServerTimeout.TotalMilliseconds,
                SendTimeout = (Int32) ServerTimeout.TotalMilliseconds
            };

            socket.Connect(requestEndPoint.Host, requestEndPoint.Port);

            return new NetworkStream(socket, true);
        }

        public void Handle(Socket clientSocket) {
            Contract.Requires<ArgumentNullException>(clientSocket != null, "clientSocket");

            RelayHttpMessage(CreateClientStream(clientSocket));
        }

        protected void ReceiveRequest(ProcessingContext context) {
            Contract.Requires<ArgumentNullException>(context != null, "context");
            Contract.Requires<ArgumentNullException>(context.ClientStream != null, "context");

            context.RequestHeaders = context.ClientStream.ReadRequestHeaders();

            if (OnRequestReceived != null) {
                OnRequestReceived(context);
            }

            context.NextStage();
        }

        protected void ConnectToServer(ProcessingContext context) {
            Contract.Requires<ArgumentNullException>(context != null, "context");
            Contract.Requires<ArgumentNullException>(context.RequestHeaders != null, "context");

            var serverEndPoint = context.RequestHeaders.ResolveRequestEndPoint(DefaultHttpPort);

            context.ServerStream = CreateServerStream(serverEndPoint);

            if (OnServerConnected != null) {
                OnServerConnected(context);
            }

            context.NextStage();
        }

        protected void ReceiveResponse(ProcessingContext context) {
            Contract.Requires<ArgumentNullException>(context != null, "context");
            Contract.Requires<ArgumentNullException>(context.ServerStream != null, "context");
            Contract.Requires<ArgumentNullException>(context.RequestHeaders != null, "context");
            Contract.Requires<ArgumentNullException>(context.ClientStream != null, "context");

            context.ServerStream.WriteHttpMessage(context.RequestHeaders, context.ClientStream, BufferSize);
            context.ResponseHeaders = context.ServerStream.ReadResponseHeaders();

            if (OnResponseReceived != null) {
                OnResponseReceived(context);
            }

            context.NextStage();
        }

        private void FinishProcessing(ProcessingContext context) {
            Contract.Requires<ArgumentNullException>(context != null, "context");

            if (context.ClientStream != null) {
                context.ClientStream.Dispose();
            }

            if (context.ServerStream != null) {
                context.ServerStream.Dispose();
            }
        }

        protected void RelayHttpMessage(Stream clientStream) {
            Contract.Requires<ArgumentNullException>(clientStream != null, "clientStream");

            var stopProcessing = false;

            using (clientStream) {
                HttpRequestHeaders requestHeaders;

                try {
                    requestHeaders = clientStream.ReadRequestHeaders();
                } catch (Exception ex) {
                    Logger.Error("Failed to read headers from client stream", ex);
                    clientStream.SendInvalidRequest(Stream.Null, BufferSize);
                    return;
                }

                Logger.InfoFormat("Request received: [{0}]", requestHeaders.StartLine);

                try {
                    HandleRequestReceived(requestHeaders, clientStream, ref stopProcessing);
                } catch (Exception ex) {
                    Logger.Error("Failed to handle received request", ex);
                    clientStream.SendInternalServerError(Stream.Null, BufferSize);
                    return;
                }

                if (stopProcessing) {
                    Logger.Info("Request processing stopped");
                    return;
                }

                DnsEndPoint requestEndPoint;

                try {
                    requestEndPoint = requestHeaders.ResolveRequestEndPoint(_defaultPort);
                } catch (Exception ex) {
                    Logger.Error("Failed to resolve request endpoint", ex);
                    clientStream.SendInvalidRequest(Stream.Null, BufferSize);
                    return;
                }

                Logger.InfoFormat("Request endpoint resolved: [{0}]", requestEndPoint);

                Stream serverStream;

                try {
                    serverStream = CreateServerStream(requestEndPoint);
                } catch (Exception ex) {
                    Logger.Error("Failed to connect to remote server", ex);
                    clientStream.SendInternalServerError(Stream.Null, BufferSize);
                    return;
                }

                using (serverStream) {
                    try {
                        HandleServerConnected(requestHeaders, clientStream, serverStream, ref stopProcessing);
                    } catch (Exception ex) {
                        Logger.Error("Failed to handle server connected", ex);
                        clientStream.SendInternalServerError(Stream.Null, BufferSize);
                        return;
                    }

                    if (stopProcessing) {
                        Logger.Info("Request processing stopped");
                        return;
                    }

                    HttpResponseHeaders responseHeaders;

                    try {
                        serverStream.WriteHttpMessage(requestHeaders, clientStream, BufferSize);
                    } catch (Exception ex) {
                        Logger.Error("Failed to send client request to server", ex);
                        clientStream.SendInternalServerError(Stream.Null, BufferSize);
                        return;
                    }

                    Logger.InfoFormat("Request [{0}] sent to server", requestHeaders.StartLine);

                    try {
                        responseHeaders = serverStream.ReadResponseHeaders();
                    } catch (Exception ex) {
                        Logger.Error("Failed to read server response", ex);
                        clientStream.SendInternalServerError(Stream.Null, BufferSize);
                        return;
                    }

                    Logger.InfoFormat("Received response from [{0}]", requestHeaders.StartLine);

                    try {
                        HandleReponseReceived(responseHeaders, clientStream, serverStream, ref stopProcessing);
                    } catch (Exception ex) {
                        Logger.Warn("Failed to handle server response", ex);
                        clientStream.SendInternalServerError(Stream.Null, BufferSize);
                        return;
                    }

                    if (stopProcessing) {
                        Logger.Info("Request processing stopped");
                        return;
                    }

                    try {
                        clientStream.WriteHttpMessage(responseHeaders, serverStream, BufferSize);
                        Logger.DebugFormat("Request processing finished [{0}]", requestHeaders.StartLine);
                    } catch (Exception ex) {
                        Logger.Error(String.Format("Failed to transfer server response to client: {0}", requestHeaders.StartLine), ex);
                    }
                }
            }
        }

        protected virtual void HandleRequestReceived(HttpRequestHeaders headers, Stream clientStream, ref Boolean stopProcessing) {
            ConnectionHeaderHandler.RemoveIfPresent(headers);

            if (OnRequestReceived == null) {
                return;
            }

            OnRequestReceived(headers);
        }

        protected virtual void HandleServerConnected(
            HttpRequestHeaders headers, Stream requestStream, Stream serverStream, ref Boolean stopProcessing) {}

        protected virtual void HandleReponseReceived(
            HttpResponseHeaders headers, Stream clientStream, Stream serverStream, ref Boolean stopProcessing) {
            if (OnResponseReceived == null) {
                return;
            }

            OnResponseReceived(headers);
        }

    }

}