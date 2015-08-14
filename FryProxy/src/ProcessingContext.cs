using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using HttpRequestHeader = FryProxy.Headers.HttpRequestHeader;
using HttpResponseHeader = FryProxy.Headers.HttpResponseHeader;

namespace FryProxy
{
    /// <summary>
    ///     Stores relevant information for processing single request
    /// </summary>
    public class ProcessingContext
    {
        /// <summary>
        ///     Exception thrown during processing request
        /// </summary>
        public Exception Exception { get; internal set; }

        /// <summary>
        ///     Current stage of request processing process
        /// </summary>
        public ProcessingStage Stage { get; internal set; }

        /// <summary>
        ///     Destination server endpoint
        /// </summary>
        public DnsEndPoint ServerEndPoint { get; set; }

        /// <summary>
        ///     Stream through which proxy communicates with it's client
        /// </summary>
        public Stream ClientStream { get; set; }

        /// <summary>
        ///     Stream used by proxy for communicating with destination server
        /// </summary>
        public Stream ServerStream { get; set; }

        /// <summary>
        ///     HTTP message header received from client
        /// </summary>
        public HttpRequestHeader RequestHeader { get; set; }

        /// <summary>
        ///     HTTP message header received from destination server
        /// </summary>
        public HttpResponseHeader ResponseHeader { get; set; }

        /// <summary>
        ///     Underlying client socket
        /// </summary>
        public Socket ClientSocket { get; internal set; }

        /// <summary>
        ///     Underlying server socket
        /// </summary>
        public Socket ServerSocket { get; internal set; }

        /// <summary>
        ///     Interrupt processing current request
        /// </summary>
        public void StopProcessing()
        {
            Stage = ProcessingStage.Completed;
        }
    }
}