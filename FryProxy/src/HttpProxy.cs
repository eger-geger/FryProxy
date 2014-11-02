using System;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.IO;
using System.Net;
using System.Net.Sockets;

using FryProxy.Utility;

using log4net;

namespace FryProxy {

    public class HttpProxy {

        private const Int32 DefaultHttpPort = 80;
        private const Int32 DefaultBufferSize = 4096;

        private static readonly TimeSpan DefaultCommunicationTimeout = TimeSpan.FromSeconds(1);

        protected readonly ILog Logger;

        private readonly Int32 _bufferSize;
        private readonly Int32 _defaultPort;

        public Action<ProcessingContext> OnProcessingComplete;
        public Action<ProcessingContext> OnRequestReceived;
        public Action<ProcessingContext> OnResponseReceived;
        public Action<ProcessingContext> OnServerConnected;
        public Action<ProcessingContext> OnResponseSent;

        public HttpProxy() : this(DefaultHttpPort) {}

        public HttpProxy(Int32 defaultPort) : this(defaultPort, DefaultBufferSize) {}

        public HttpProxy(Int32 defaultPort, Int32 bufferSize) {
            Contract.Requires<ArgumentOutOfRangeException>(bufferSize > 0, "bufferSize");
            Contract.Requires<ArgumentOutOfRangeException>(defaultPort > IPEndPoint.MinPort && defaultPort < IPEndPoint.MaxPort, "defaultPort");

            Logger = LogManager.GetLogger(GetType());

            _defaultPort = defaultPort;
            _bufferSize = bufferSize;

            ClientReadTimeout = DefaultCommunicationTimeout;
            ClientWriteTimeout = DefaultCommunicationTimeout;
            ServerReadTimeout = DefaultCommunicationTimeout;
            ServerReadTimeout = DefaultCommunicationTimeout;
        }

        public TimeSpan ClientReadTimeout { get; set; }

        public TimeSpan ClientWriteTimeout { get; set; }

        public TimeSpan ServerReadTimeout { get; set; }

        public TimeSpan ServerWriteTimeout { get; set; }

        public void Handle(Socket clientSocket) {
            Contract.Requires<ArgumentNullException>(clientSocket != null, "clientSocket");

            var pipeLine = new ProcessingPipeLine(
                new Dictionary<ProcessingStage, Action<ProcessingContext>> {
                    {ProcessingStage.ReceiveRequest, ReceiveRequest + OnRequestReceived},
                    {ProcessingStage.ConnectToServer, ConnectToServer + OnServerConnected},
                    {ProcessingStage.ReceiveResponse, ReceiveResponse + OnResponseReceived},
                    {ProcessingStage.Completed, CompleteProcessing + OnProcessingComplete},
                    {ProcessingStage.SendResponse, SendResponse + OnResponseSent}
                });

            var clientStream = new NetworkStream(clientSocket, true) {
                ReadTimeout = (Int32) ClientReadTimeout.TotalMilliseconds,
                WriteTimeout = (Int32) ClientWriteTimeout.TotalMilliseconds,
            };

            var processingContex = new ProcessingContext(pipeLine) {
                ClientStream = clientStream
            };

            try {
                pipeLine.Start(processingContex);
            } catch (Exception ex) {
                Logger.Error("Request proccessing failed", ex);
                clientStream.SendInternalServerError(Stream.Null, _bufferSize);
            } finally {
                clientSocket.Dispose();
            }
        }

        protected virtual void ReceiveRequest(ProcessingContext context) {
            Contract.Requires<ArgumentNullException>(context != null, "context");
            Contract.Requires<ArgumentNullException>(context.ClientStream != null, "context");

            context.RequestHeaders = context.ClientStream.ReadRequestHeaders();

            Logger.InfoFormat("Request [{0}] received", context.RequestHeaders.StartLine);
        }

        protected virtual void ConnectToServer(ProcessingContext context) {
            Contract.Requires<ArgumentNullException>(context != null, "context");
            Contract.Requires<ArgumentNullException>(context.RequestHeaders != null, "context");

            var serverEndPoint = context.RequestHeaders.ResolveRequestEndPoint(_defaultPort);

            var serverSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp) {
                ReceiveTimeout = (Int32) ServerReadTimeout.TotalMilliseconds,
                SendTimeout = (Int32) ServerWriteTimeout.TotalMilliseconds
            };

            serverSocket.Connect(serverEndPoint.Host, serverEndPoint.Port);

            context.ServerEndPoint = serverEndPoint;
            context.ServerStream = new NetworkStream(serverSocket, true);

            Logger.InfoFormat("Connection to [{0}] established", serverEndPoint);
        }

        protected virtual void ReceiveResponse(ProcessingContext context) {
            Contract.Requires<ArgumentNullException>(context != null, "context");
            Contract.Requires<ArgumentNullException>(context.ServerStream != null, "context");
            Contract.Requires<ArgumentNullException>(context.RequestHeaders != null, "context");
            Contract.Requires<ArgumentNullException>(context.ClientStream != null, "context");

            context.ServerStream.WriteHttpMessage(context.RequestHeaders, context.ClientStream, _bufferSize);
            context.ResponseHeaders = context.ServerStream.ReadResponseHeaders();

            Logger.InfoFormat("Response received [{0}]", context.ResponseHeaders.StartLine);
        }

        protected virtual void SendResponse(ProcessingContext context) {
            Contract.Requires<ArgumentNullException>(context != null, "context");
            Contract.Requires<InvalidOperationException>(context.ServerStream != null, "context#ServerStream should not be null");
            Contract.Requires<InvalidOperationException>(context.ResponseHeaders != null, "context#ResponseHeaders should not be null");
            Contract.Requires<InvalidOperationException>(context.ClientStream != null, "context#ClientStream should not be null");

            context.ClientStream.WriteHttpMessage(context.ResponseHeaders, context.ServerStream, _bufferSize);

            Logger.InfoFormat("Response [{0}] send", context.ResponseHeaders.StartLine);
        }

        protected virtual void CompleteProcessing(ProcessingContext context) {
            Contract.Requires<ArgumentNullException>(context != null, "context");

            if (context.ClientStream != null) {
                context.ClientStream.Dispose();
            }

            if (context.ServerStream != null) {
                context.ServerStream.Dispose();
            }

            if (OnProcessingComplete != null) {
                OnProcessingComplete(context);
            }

            Logger.InfoFormat("Request [{0}] processing complete", context.RequestHeaders.StartLine);
        }

    }

}