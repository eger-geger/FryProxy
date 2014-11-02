using System;
using System.Diagnostics.Contracts;
using System.IO;
using System.Net;

using FryProxy.Headers;

namespace FryProxy {

    public class ProcessingContext {

        private readonly ProcessingPipeLine _pipeLine;

        internal ProcessingContext(ProcessingPipeLine pipeLine) {
            Contract.Requires<ArgumentNullException>(pipeLine != null, "pipeLine");

            _pipeLine = pipeLine;
        }

        public ProcessingStage Stage {
            get { return _pipeLine.CurrentStage; }
        }

        public DnsEndPoint ServerEndPoint { get; set; }

        public Stream ClientStream { get; set; }

        public Stream ServerStream { get; set; }

        public HttpRequestHeaders RequestHeaders { get; set; }

        public HttpResponseHeaders ResponseHeaders { get; set; }

        public void StopProcessing() {
            _pipeLine.Stop();
        }

    }

}