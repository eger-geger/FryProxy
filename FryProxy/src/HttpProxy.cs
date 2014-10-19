using System;
using System.Diagnostics.Contracts;
using System.IO;
using System.Net;
using System.Net.Sockets;

using FryProxy.Handlers;
using FryProxy.HttpMessage;
using FryProxy.Utility;

using log4net;

namespace FryProxy {

    public class HttpProxy {

        public delegate void RequestHandler(RequestMessage message);

        public delegate void ResponseHandler(ResponseMessage message);

        private const Int32 DefaultHttpPort = 80;

        protected readonly ILog Logger;

        private readonly Int32 _defaultPort;

        public RequestHandler OnRequest;

        public ResponseHandler OnResponse;

        public HttpProxy() : this(DefaultHttpPort) {}

        public HttpProxy(Int32 defaultPort) {
            Contract.Requires<ArgumentOutOfRangeException>(defaultPort > IPEndPoint.MinPort && defaultPort < IPEndPoint.MaxPort, "defaultPort");

            _defaultPort = defaultPort;

            Logger = LogManager.GetLogger(GetType());

            ClientTimeout = TimeSpan.FromSeconds(1);
            ServerTimeout = TimeSpan.FromSeconds(1);
        }

        public TimeSpan ClientTimeout { get; set; }

        public TimeSpan ServerTimeout { get; set; }

        protected virtual Stream CreateClientStream(Socket clientSocket) {
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

            using (var clientStream = CreateClientStream(clientSocket)) {
                RequestMessage requestMessage;

                try {
                    requestMessage = clientStream.ReadRequestMessage();
                } catch (Exception ex) {
                    Logger.Warn("Failed to read client request", ex);
                    SendInvalidRequest(clientStream, ex.Message);
                    return;
                }

                Logger.DebugFormat("request received: [{0}]", requestMessage.StartLine);

                try {
                    HandleRequestMessage(requestMessage);
                } catch (Exception ex) {
                    Logger.Warn("Failed to handle request", ex);
                    SendInternalServerError(clientStream, ex.Message);
                    return;
                }

                if (!clientSocket.Connected) {
                    Logger.InfoFormat("Request [{0}] aborted", requestMessage.StartLine);
                    return;
                }

                DnsEndPoint requestEndPoint;

                try {
                    requestEndPoint = requestMessage.ResolveRequestEndPoint(_defaultPort);
                } catch (Exception ex) {
                    Logger.Warn("Failed to resolve request endpoint", ex);
                    SendInvalidRequest(clientStream, ex.Message);
                    return;
                }

                Logger.DebugFormat("request endpoint resolved: [{0}]", requestEndPoint);

                Stream serverStream;

                try {
                    serverStream = CreateServerStream(requestEndPoint);
                } catch (Exception ex) {
                    Logger.Error("Failed to connect to remote server", ex);
                    SendInternalServerError(clientStream, ex.Message);
                    return;
                }

                using (serverStream) {
                    ResponseMessage responseMessage;

                    try {
                        serverStream.Write(requestMessage);
                    } catch (Exception ex) {
                        Logger.Error("Failed to send client request to server", ex);
                        SendInternalServerError(clientStream, ex.Message);
                        return;
                    }

                    Logger.DebugFormat("request [{0}] sent", requestMessage.StartLine);

                    try {
                        responseMessage = serverStream.ReadResponseMessage();
                    } catch (Exception ex) {
                        Logger.Error("Failed to read server response", ex);
                        SendInternalServerError(clientStream, ex.Message);
                        return;
                    }

                    Logger.DebugFormat("received response from [{0}]", requestMessage.StartLine);

                    try {
                        HandleResponseMessage(responseMessage);
                    } catch (Exception ex) {
                        Logger.Warn("Failed to handle server response", ex);
                        SendInternalServerError(clientStream, ex.Message);
                        return;
                    }

                    try {
                        clientStream.Write(responseMessage);
                        Logger.DebugFormat("[{0}] delivered", requestMessage.StartLine);
                    } catch (Exception ex) {
                        Logger.Error(String.Format("Failed to transfer server response to client: {0}", requestMessage.StartLine), ex);
                    }
                }
            }
        }

        private void SendInternalServerError(Stream stream, String message) {
            Contract.Requires<ArgumentNullException>(stream != null, "client");

            var httpMessage = ResponseMessageFactory.CreatePlainTextResponse(500, "Internal Server Error", message);

            try {
                stream.Write(httpMessage);
            } catch (Exception ex) {
                Logger.Warn("Failed to respond", ex);
            }
        }

        private void SendInvalidRequest(Stream stream, String message) {
            Contract.Requires<ArgumentNullException>(stream != null, "client");

            var httpMessage = ResponseMessageFactory.CreatePlainTextResponse(400, message, message);

            try {
                stream.Write(httpMessage);
            } catch (Exception ex) {
                Logger.Warn("Failed to respond", ex);
            }
        }

        private void HandleRequestMessage(RequestMessage message) {
            ConnectionHeaderHandler.RemoveIfPresent(message);

            if (OnRequest == null) {
                return;
            }

            Logger.DebugFormat("processing request: {0}", message.StartLine);
            OnRequest(message);
            Logger.DebugFormat("request processed: {0}", message.StartLine);
        }

        private void HandleResponseMessage(ResponseMessage message) {
            ConnectionHeaderHandler.RemoveIfPresent(message);

            if (OnResponse == null) {
                return;
            }

            Logger.DebugFormat("processing response: {0}", message.StartLine);
            OnResponse(message);
            Logger.DebugFormat("response processed: {0}", message.StartLine);
        }

    }

}