using System;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.IO;
using System.Net;
using System.Net.Sockets;

using FryProxy.Utility;

using log4net;

namespace FryProxy {

    /// <summary>
    ///     Process incoming HTTP request and provides interface for intercepting it at different stages.
    /// </summary>
    public class HttpProxy {

        protected const Int32 DefaultHttpPort = 80;
        protected const Int32 DefaultBufferSize = 4096;

        private static readonly TimeSpan DefaultCommunicationTimeout = TimeSpan.FromSeconds(1);

        protected readonly ILog Logger;

        private readonly Int32 _bufferSize;
        private readonly Int32 _defaultPort;

        /// <summary>
        ///     Called when all other stages of request processing are done. 
        ///     All <see cref="ProcessingContext"/> information should be available now.
        /// </summary>
        public Action<ProcessingContext> OnProcessingComplete;

        /// <summary>
        ///     Called when request from client is received by proxy. 
        ///     <see cref="ProcessingContext.RequestHeaders"/> and <see cref="ProcessingContext.ClientStream"/> are available at this stage.
        /// </summary>
        public Action<ProcessingContext> OnRequestReceived;

        /// <summary>
        ///     Called when response from destination server is received by proxy. 
        ///     <see cref="ProcessingContext.ResponseHeaders"/> is added at this stage.
        /// </summary>
        public Action<ProcessingContext> OnResponseReceived;

        /// <summary>
        ///     Called when proxy has established connection to destination server.
        ///     <see cref="ProcessingContext.ServerEndPoint"/> and <see cref="ProcessingContext.ServerStream"/> are defined at this stage.
        /// </summary>
        public Action<ProcessingContext> OnServerConnected;

        /// <summary>
        ///     Called when server response has been relayed to client.
        ///     All <see cref="ProcessingContext"/> information should be available.
        /// </summary>
        public Action<ProcessingContext> OnResponseSent;

        /// <summary>
        ///     Creates new instance of <see cref="HttpProxy"/> using default HTTP port (80).
        /// </summary>
        public HttpProxy() : this(DefaultHttpPort) {}

        /// <summary>
        ///     Creates new instance of <see cref="HttpProxy"/> using provided default port.
        /// </summary>
        /// <param name="defaultPort">
        ///     Port number on destination server which will be used if not specified in request
        /// </param>
        public HttpProxy(Int32 defaultPort) : this(defaultPort, DefaultBufferSize) {}

        /// <summary>
        ///     Creates new instance of <see cref="HttpProxy"/> using provided default port and internal buffer size.
        /// </summary>
        /// <param name="defaultPort">
        ///     Port number on destination server which will be used if not specified in request
        /// </param>
        /// <param name="bufferSize">
        ///     Size of buffer used internaly for copying streams
        /// </param>
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

        /// <summary>
        ///     Client socket read timeout
        /// </summary>
        public TimeSpan ClientReadTimeout { get; set; }

        /// <summary>
        ///     Client socket write timeout
        /// </summary>
        public TimeSpan ClientWriteTimeout { get; set; }

        /// <summary>
        ///     Server socket read timeout
        /// </summary>
        public TimeSpan ServerReadTimeout { get; set; }

        /// <summary>
        ///     Server socket write timeout
        /// </summary>
        public TimeSpan ServerWriteTimeout { get; set; }

        /// <summary>
        ///     Accept client connection, create <see cref="ProcessingContext"/> and <see cref="ProcessingContext.ClientStream"/> and start processing request. 
        /// </summary>
        /// <param name="clientSocket">Socket opened by the client</param>
        public void HandleClient(Socket clientSocket) {
            Contract.Requires<ArgumentNullException>(clientSocket != null, "clientSocket");

            var pipeline = new ProcessingPipeline(
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

            var processingContex = new ProcessingContext(pipeline) {
                ClientStream = clientStream
            };

            try {
                pipeline.Start(processingContex);
            } catch (Exception ex) {
                Logger.Error("Request proccessing failed", ex);
                clientStream.SendInternalServerError(Stream.Null, _bufferSize);
            }
        }

        /// <summary>
        ///     Read <see cref="ProcessingContext.RequestHeaders"/> from <see cref="ProcessingContext.ClientStream"/>.
        ///     <see cref="ProcessingContext.ClientStream"/> should be defined at this point.
        /// </summary>
        /// <param name="context">current request context</param>
        protected virtual void ReceiveRequest(ProcessingContext context) {
            Contract.Requires<ArgumentNullException>(context != null, "context");
            Contract.Requires<InvalidContextException>(context.ClientStream != null, "ClientStream");

            context.RequestHeaders = context.ClientStream.ReadRequestHeaders();

            Logger.InfoFormat("Request [{0}] received", context.RequestHeaders.StartLine);
        }

        /// <summary>
        ///     Resolve <see cref="ProcessingContext.ServerEndPoint"/> based on <see cref="ProcessingContext.RequestHeaders"/>,
        ///     establish connection to destination server and open <see cref="ProcessingContext.ServerStream"/>.
        ///     <see cref="ProcessingContext.RequestHeaders"/> should be defined.
        /// </summary>
        /// <param name="context">current request context</param>
        protected virtual void ConnectToServer(ProcessingContext context) {
            Contract.Requires<ArgumentNullException>(context != null, "context");
            Contract.Requires<InvalidContextException>(context.RequestHeaders != null, "RequestHeaders");

            var serverEndPoint = context.RequestHeaders.ResolveRequestEndpoint(_defaultPort);

            var serverSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp) {
                ReceiveTimeout = (Int32) ServerReadTimeout.TotalMilliseconds,
                SendTimeout = (Int32) ServerWriteTimeout.TotalMilliseconds
            };

            serverSocket.Connect(serverEndPoint.Host, serverEndPoint.Port);

            context.ServerEndPoint = serverEndPoint;
            context.ServerStream = new NetworkStream(serverSocket, true);

            Logger.InfoFormat("Connection to [{0}] established", serverEndPoint);
        }

        /// <summary>
        ///     Send <see cref="ProcessingContext.RequestHeaders"/> to server, 
        ///     copy rest of the <see cref="ProcessingContext.ClientStream"/> to <see cref="ProcessingContext.ServerStream"/>
        ///     and read <see cref="ProcessingContext.ResponseHeaders"/> from <see cref="ProcessingContext.ServerStream"/>.
        ///     Expects <see cref="ProcessingContext.ServerStream"/>, <see cref="ProcessingContext.RequestHeaders"/> and 
        ///     <see cref="ProcessingContext.ClientStream"/> to be defined.
        /// </summary>
        /// <param name="context">current request context</param>
        protected virtual void ReceiveResponse(ProcessingContext context) {
            Contract.Requires<ArgumentNullException>(context != null, "context");
            Contract.Requires<InvalidContextException>(context.ServerStream != null, "ServerStream");
            Contract.Requires<InvalidContextException>(context.RequestHeaders != null, "RequestHeaders");
            Contract.Requires<InvalidContextException>(context.ClientStream != null, "ClientStream");

            context.ServerStream.WriteHttpMessage(context.RequestHeaders, context.ClientStream, _bufferSize);
            context.ResponseHeaders = context.ServerStream.ReadResponseHeaders();

            Logger.InfoFormat("Response received [{0}]", context.ResponseHeaders.StartLine);
        }

        /// <summary>
        ///     Send respose to <see cref="ProcessingContext.ClientStream"/> containing <see cref="ProcessingContext.ResponseHeaders"/>
        ///     and rest of<see cref="ProcessingContext.ServerStream"/>.
        ///     Expect <see cref="ProcessingContext.ServerStream"/>, <see cref="ProcessingContext.ClientStream"/> and
        ///     <see cref="ProcessingContext.ResponseHeaders"/> to be defined.
        /// </summary>
        /// <param name="context">current request context</param>
        protected virtual void SendResponse(ProcessingContext context) {
            Contract.Requires<ArgumentNullException>(context != null, "context");
            Contract.Requires<InvalidOperationException>(context.ServerStream != null, "ServerStream");
            Contract.Requires<InvalidOperationException>(context.ResponseHeaders != null, "ResponseHeaders");
            Contract.Requires<InvalidOperationException>(context.ClientStream != null, "ClientStream");

            context.ClientStream.WriteHttpMessage(context.ResponseHeaders, context.ServerStream, _bufferSize);

            Logger.InfoFormat("Response [{0}] send", context.ResponseHeaders.StartLine);
        }

        /// <summary>
        ///     Close client and server connections.
        ///     Expect <see cref="ProcessingContext.ClientStream"/> and <see cref="ProcessingContext.ServerStream"/> to be defined.
        /// </summary>
        /// <param name="context"></param>
        protected virtual void CompleteProcessing(ProcessingContext context) {
            Contract.Requires<ArgumentNullException>(context != null, "context");

            if (context.ClientStream != null) {
                context.ClientStream.Dispose();
            }

            if (context.ServerStream != null) {
                context.ServerStream.Dispose();
            }

            Logger.InfoFormat("Request [{0}] processing complete", context.RequestHeaders.StartLine);
        }

    }

}