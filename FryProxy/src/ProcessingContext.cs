using System;
using System.Diagnostics.Contracts;
using System.IO;
using System.Net;

using FryProxy.Headers;

namespace FryProxy {

    /// <summary>
    ///     Stores relevant information for processing single request
    /// </summary>
    public class ProcessingContext {

        private readonly ProcessingPipeline _pipeline;

        internal ProcessingContext(ProcessingPipeline pipeline) {
            Contract.Requires<ArgumentNullException>(pipeline != null, "pipeLine");

            _pipeline = pipeline;
        }

        /// <summary>
        ///     Current stage of request processing process
        /// </summary>
        public ProcessingStage Stage {
            get { return _pipeline.CurrentStage; }
        }

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
        public HttpRequestHeaders RequestHeaders { get; set; }

        /// <summary>
        ///     HTTP message header received from destination server
        /// </summary>
        public HttpResponseHeaders ResponseHeaders { get; set; }

        /// <summary>
        ///     Interrupt processing current request
        /// </summary>
        public void StopProcessing() {
            _pipeline.Stop();
        }

    }

}