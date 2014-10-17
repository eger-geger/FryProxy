using System;
using System.Diagnostics.Contracts;
using System.IO;
using System.Net;
using System.Net.Sockets;

using FryProxy;
using FryProxy.HttpMessage;

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
        }

        public TimeSpan ClientTimeout { get; set; }

        public TimeSpan ServerTimeout { get; set; }

        protected virtual Stream CreateClientStream(Socket clientSocket) {
            clientSocket.ReceiveTimeout = (Int32) ClientTimeout.TotalMilliseconds;
            clientSocket.SendTimeout = (Int32) ClientTimeout.TotalMilliseconds;

            return new NetworkStream(clientSocket);
        }

        protected virtual Stream CreateServerStream(DnsEndPoint requestEndPoint) {
            var socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp) {
                ReceiveTimeout = (Int32) ServerTimeout.TotalMilliseconds,
                SendTimeout = (Int32) ServerTimeout.TotalMilliseconds
            };

            socket.Connect(requestEndPoint.Host, requestEndPoint.Port);

            return new NetworkStream(socket);
        }

        public void Handle(Socket clientSocket) {
            using (var clientStream = CreateClientStream(clientSocket)) {
                RequestMessage requestMessage;

                try {
                    requestMessage = clientStream.ReadRequestMessage();
                } catch (Exception ex) {
                    Logger.Warn("Failed to read client request", ex);
                    SendInvalidRequest(clientStream, ex.Message);
                    return;
                }

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

                using (var serverStream = CreateServerStream(requestEndPoint)) {
                    ResponseMessage responseMessage;

                    try {
                        serverStream.Write(requestMessage);
                    } catch (Exception ex) {
                        Logger.Error("Failed to transfer client request to server", ex);
                        SendInternalServerError(clientStream, ex.Message);
                        return;
                    }

                    try {
                        responseMessage = serverStream.ReadResponseMessage();
                    } catch (Exception ex) {
                        Logger.Error("Failed to read server response", ex);
                        SendInternalServerError(clientStream, ex.Message);
                        return;
                    }

                    try {
                        HandleResponseMessage(responseMessage);
                    } catch (Exception ex) {
                        Logger.Warn("Failed to handle server response", ex);
                        SendInternalServerError(clientStream, ex.Message);
                        return;
                    }

                    try {
                        clientStream.Write(responseMessage);
                    } catch (Exception ex) {
                        Logger.Error("Failed to transfer server response to client", ex);
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
            if (OnRequest != null) {
                OnRequest(message);
            }
        }

        private void HandleResponseMessage(ResponseMessage message) {
            if (OnResponse != null) {
                OnResponse(message);
            }
        }

    }

}