using System;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.IO;
using System.Net;
using System.Net.Sockets;
using FryProxy.Readers;
using FryProxy.Utils;
using FryProxy.Writers;
using log4net;
using HttpRequestHeader = FryProxy.Headers.HttpRequestHeader;
using HttpResponseHeader = FryProxy.Headers.HttpResponseHeader;

namespace FryProxy
{
    /// <summary>
    ///     Process incoming HTTP request and provides interface for intercepting it at different stages.
    /// </summary>
    public class HttpProxy
    {
        protected const Int32 DefaultHttpPort = 80;

        protected static readonly ILog Logger = LogManager.GetLogger(typeof (HttpProxy));

        private static readonly TimeSpan DefaultCommunicationTimeout = TimeSpan.FromSeconds(1);

        private readonly Int32 _defaultPort;

        private readonly ActionWrapper<ProcessingContext> _onProcessingCompleteWrapper =
            new ActionWrapper<ProcessingContext>();

        private readonly ActionWrapper<ProcessingContext> _onRequestReceivedWrapper =
            new ActionWrapper<ProcessingContext>();

        private readonly ActionWrapper<ProcessingContext> _onResponseReceivedWrapper =
            new ActionWrapper<ProcessingContext>();

        private readonly ActionWrapper<ProcessingContext> _onResponseSentWrapper =
            new ActionWrapper<ProcessingContext>();

        private readonly ActionWrapper<ProcessingContext> _onServerConnectedWrapper =
            new ActionWrapper<ProcessingContext>();

        private readonly ProcessingPipeline _pipeline;

        /// <summary>
        ///     Creates new instance of <see cref="HttpProxy" /> using default HTTP port (80).
        /// </summary>
        public HttpProxy() : this(DefaultHttpPort)
        {
        }

        /// <summary>
        ///     Creates new instance of <see cref="HttpProxy" /> using provided default port and internal buffer size.
        /// </summary>
        /// <param name="defaultPort">
        ///     Port number on destination server which will be used if not specified in request
        /// </param>
        public HttpProxy(Int32 defaultPort)
        {
            Contract.Requires<ArgumentOutOfRangeException>(
                defaultPort > IPEndPoint.MinPort && defaultPort < IPEndPoint.MaxPort, "defaultPort");

            _defaultPort = defaultPort;

            ClientReadTimeout = DefaultCommunicationTimeout;
            ClientWriteTimeout = DefaultCommunicationTimeout;
            ServerReadTimeout = DefaultCommunicationTimeout;
            ServerWriteTimeout = DefaultCommunicationTimeout;

            _pipeline = new ProcessingPipeline(new Dictionary<ProcessingStage, Action<ProcessingContext>>
            {
                {ProcessingStage.ReceiveRequest, ReceiveRequest + _onRequestReceivedWrapper},
                {ProcessingStage.ConnectToServer, ConnectToServer + _onServerConnectedWrapper},
                {ProcessingStage.ReceiveResponse, ReceiveResponse + _onResponseReceivedWrapper},
                {ProcessingStage.Completed, CompleteProcessing + _onProcessingCompleteWrapper},
                {ProcessingStage.SendResponse, SendResponse + _onResponseSentWrapper}
            });
        }

        /// <summary>
        ///     Called when all other stages of request processing are done.
        ///     All <see cref="ProcessingContext" /> information should be available now.
        /// </summary>
        public Action<ProcessingContext> OnProcessingComplete
        {
            get { return _onProcessingCompleteWrapper.Action; }
            set { _onProcessingCompleteWrapper.Action = value; }
        }

        /// <summary>
        ///     Called when request from client is received by proxy.
        ///     <see cref="ProcessingContext.RequestHeader" /> and <see cref="ProcessingContext.ClientStream" /> are available at
        ///     this stage.
        /// </summary>
        public Action<ProcessingContext> OnRequestReceived
        {
            get { return _onRequestReceivedWrapper.Action; }
            set { _onRequestReceivedWrapper.Action = value; }
        }

        /// <summary>
        ///     Called when response from destination server is received by proxy.
        ///     <see cref="ProcessingContext.ResponseHeader" /> is added at this stage.
        /// </summary>
        public Action<ProcessingContext> OnResponseReceived
        {
            get { return _onResponseReceivedWrapper.Action; }
            set { _onResponseReceivedWrapper.Action = value; }
        }

        /// <summary>
        ///     Called when server response has been relayed to client.
        ///     All <see cref="ProcessingContext" /> information should be available.
        /// </summary>
        public Action<ProcessingContext> OnResponseSent
        {
            get { return _onResponseSentWrapper.Action; }
            set { _onResponseSentWrapper.Action = value; }
        }

        /// <summary>
        ///     Called when proxy has established connection to destination server.
        ///     <see cref="ProcessingContext.ServerEndPoint" /> and <see cref="ProcessingContext.ServerStream" /> are defined at
        ///     this stage.
        /// </summary>
        public Action<ProcessingContext> OnServerConnected
        {
            get { return _onServerConnectedWrapper.Action; }
            set { _onServerConnectedWrapper.Action = value; }
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
        ///     Accept client connection, create <see cref="ProcessingContext" /> and <see cref="ProcessingContext.ClientStream" />
        ///     and start processing request.
        /// </summary>
        /// <param name="clientSocket">Socket opened by the client</param>
        public void HandleClient(Socket clientSocket)
        {
            Contract.Requires<ArgumentNullException>(clientSocket != null, "clientSocket");

            var context = new ProcessingContext
            {
                ClientSocket = clientSocket,
                ClientStream = new NetworkStream(clientSocket, true)
                {
                    ReadTimeout = (Int32) ClientReadTimeout.TotalMilliseconds,
                    WriteTimeout = (Int32) ClientWriteTimeout.TotalMilliseconds
                }
            };

            _pipeline.Start(context);

            if (context.Exception != null)
            {
                Logger.Error("Failed to process request", context.Exception);
            }
        }

        /// <summary>
        ///     Read <see cref="ProcessingContext.RequestHeader" /> from <see cref="ProcessingContext.ClientStream" />.
        ///     <see cref="ProcessingContext.ClientStream" /> should be defined at this point.
        /// </summary>
        /// <param name="context">current request context</param>
        protected virtual void ReceiveRequest(ProcessingContext context)
        {
            Contract.Requires<ArgumentNullException>(context != null, "context");
            Contract.Requires<InvalidContextException>(context.ClientStream != null, "ClientStream");

            var headerReader = new HttpHeaderReader(new PlainStreamReader(context.ClientStream));

            try
            {
                context.RequestHeader = new HttpRequestHeader(headerReader.ReadHttpMessageHeader());

                if (Logger.IsDebugEnabled)
                {
                    Logger.Debug("Request received");
                    Logger.Debug(context.RequestHeader);
                }
            }
            catch (EndOfStreamException)
            {
                context.StopProcessing();

                if (Logger.IsDebugEnabled)
                {
                    Logger.Debug("Request timed out");
                }

                new HttpResponseWriter(context.ClientStream).WriteRequestTimeout();
            }
        }

        /// <summary>
        ///     Resolve <see cref="ProcessingContext.ServerEndPoint" /> based on <see cref="ProcessingContext.RequestHeader" />,
        ///     establish connection to destination server and open <see cref="ProcessingContext.ServerStream" />.
        ///     <see cref="ProcessingContext.RequestHeader" /> should be defined.
        /// </summary>
        /// <param name="context">current request context</param>
        protected virtual void ConnectToServer(ProcessingContext context)
        {
            Contract.Requires<ArgumentNullException>(context != null, "context");
            Contract.Requires<InvalidContextException>(context.RequestHeader != null, "RequestHeader");

            context.ServerEndPoint = DnsUtils.ResolveRequestEndpoint(context.RequestHeader, _defaultPort);

            context.ServerSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp)
            {
                ReceiveTimeout = (Int32) ServerReadTimeout.TotalMilliseconds,
                SendTimeout = (Int32) ServerWriteTimeout.TotalMilliseconds
            };

            context.ServerSocket.Connect(context.ServerEndPoint.Host, context.ServerEndPoint.Port);

            context.ServerStream = new NetworkStream(context.ServerSocket, true);

            if (Logger.IsDebugEnabled)
            {
                Logger.DebugFormat("Connection established: {0}:{1}",
                    context.ServerEndPoint.Host,
                    context.ServerEndPoint.Port
                    );
            }
        }

        /// <summary>
        ///     Send <see cref="ProcessingContext.RequestHeader" /> to server,
        ///     copy rest of the <see cref="ProcessingContext.ClientStream" /> to <see cref="ProcessingContext.ServerStream" />
        ///     and read <see cref="ProcessingContext.ResponseHeader" /> from <see cref="ProcessingContext.ServerStream" />.
        ///     Expects <see cref="ProcessingContext.ServerStream" />, <see cref="ProcessingContext.RequestHeader" /> and
        ///     <see cref="ProcessingContext.ClientStream" /> to be defined.
        /// </summary>
        /// <param name="context">current request context</param>
        protected virtual void ReceiveResponse(ProcessingContext context)
        {
            Contract.Requires<ArgumentNullException>(context != null, "context");
            Contract.Requires<InvalidContextException>(context.ServerStream != null, "ServerStream");
            Contract.Requires<InvalidContextException>(context.RequestHeader != null, "RequestHeader");
            Contract.Requires<InvalidContextException>(context.ClientStream != null, "ClientStream");
            Contract.Requires<InvalidContextException>(context.ClientSocket != null, "ClientSocket");

            var requestWriter = new HttpMessageWriter(context.ServerStream);
            var responseReader = new HttpHeaderReader(new PlainStreamReader(context.ServerStream));

            try
            {
                requestWriter.Write(context.RequestHeader, context.ClientStream, context.ClientSocket.Available);
                context.ResponseHeader = new HttpResponseHeader(responseReader.ReadHttpMessageHeader());

                if (Logger.IsDebugEnabled)
                {
                    Logger.Debug("Response received");
                    Logger.Debug(context.ResponseHeader);
                }
            }
            catch (IOException ex)
            {
                context.StopProcessing();

                var responseWriter = new HttpResponseWriter(context.ClientStream);

                if (SocketUtils.IsSocketException(ex, SocketError.TimedOut))
                {
                    responseWriter.WriteGatewayTimeout();

                    Logger.WarnFormat("Request to {0}:{1} has timed out",
                        context.ServerEndPoint.Host,
                        context.ServerEndPoint.Port
                        );
                }
                else
                {
                    if (Logger.IsDebugEnabled)
                    {
                        Logger.Debug("Failed to receive server response");
                        Logger.Debug(context.RequestHeader);    
                    }

                    throw;
                }
            }
        }

        /// <summary>
        ///     Send respose to <see cref="ProcessingContext.ClientStream" /> containing
        ///     <see cref="ProcessingContext.ResponseHeader" />
        ///     and rest of<see cref="ProcessingContext.ServerStream" />.
        ///     Expect <see cref="ProcessingContext.ServerStream" />, <see cref="ProcessingContext.ClientStream" /> and
        ///     <see cref="ProcessingContext.ResponseHeader" /> to be defined.
        /// </summary>
        /// <param name="context">current request context</param>
        protected virtual void SendResponse(ProcessingContext context)
        {
            Contract.Requires<ArgumentNullException>(context != null, "context");
            Contract.Requires<InvalidContextException>(context.ServerStream != null, "ServerStream");
            Contract.Requires<InvalidContextException>(context.ResponseHeader != null, "ResponseHeader");
            Contract.Requires<InvalidContextException>(context.ClientStream != null, "ClientStream");
            Contract.Requires<InvalidContextException>(context.ServerSocket != null, "ServerSocket");

            var responseWriter = new HttpResponseWriter(context.ClientStream);

            try
            {
                responseWriter.Write(context.ResponseHeader, context.ServerStream, context.ServerSocket.Available);

                if (Logger.IsDebugEnabled)
                {
                    Logger.Debug("Response sent");
                    Logger.Debug(context.ResponseHeader);
                }
            }
            catch (IOException ex)
            {
                context.StopProcessing();

                if (SocketUtils.IsSocketException(ex, SocketError.TimedOut))
                {
                    responseWriter.WriteGatewayTimeout();

                    Logger.WarnFormat("Request to {0}:{1} has timed out",
                        context.ServerEndPoint.Host,
                        context.ServerEndPoint.Port
                    );
                }
                else if (SocketUtils.IsSocketException(ex, SocketError.ConnectionReset, SocketError.ConnectionAborted))
                {
                    if (Logger.IsDebugEnabled)
                    {
                        Logger.Debug("Request aborted");
                    }
                }
                else
                {
                    if (Logger.IsDebugEnabled)
                    {
                        Logger.Debug("Failed to send response", ex);
                        Logger.Debug(context.ResponseHeader);    
                    }

                    throw;
                }
            }
        }

        /// <summary>
        ///     Close client and server connections.
        ///     Expect <see cref="ProcessingContext.ClientStream" /> and <see cref="ProcessingContext.ServerStream" /> to be
        ///     defined.
        /// </summary>
        /// <param name="context"></param>
        protected virtual void CompleteProcessing(ProcessingContext context)
        {
            Contract.Requires<ArgumentNullException>(context != null, "context");

            if (context.ClientStream != null)
            {
                context.ClientStream.Close();
            }

            if (context.ServerStream != null)
            {
                context.ServerStream.Close();
            }

            if (Logger.IsDebugEnabled)
            {
                Logger.DebugFormat("Request processing complete: {0}", context.RequestHeader.StartLine);
            }
        }
    }
}