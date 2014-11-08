using System;
using System.Diagnostics.Contracts;
using System.IO;
using System.Net;

using FryProxy.Headers;

namespace FryProxy {

    public class ProcessingContext {

        private readonly ProcessingPipeline _pipeline;

        internal ProcessingContext(ProcessingPipeline pipeline) {
            Contract.Requires<ArgumentNullException>(pipeline != null, "pipeLine");

            _pipeline = pipeline;
        }

        public ProcessingStage Stage {
            get { return _pipeline.CurrentStage; }
        }

        public DnsEndPoint ServerEndPoint { get; set; }

        public Stream ClientStream { get; set; }

        public Stream ServerStream { get; set; }

        public HttpRequestHeaders RequestHeaders { get; set; }

        public HttpResponseHeaders ResponseHeaders { get; set; }

        public void StopProcessing() {
            _pipeline.Stop();
        }

    }

}