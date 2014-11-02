using System.IO;

using FryProxy.Headers;

namespace FryProxy {

    public class ProcessingContext {

        private ProcessingStage _currentStage = ProcessingStage.ReceiveRequest;

        public ProcessingStage Stage {
            get { return _currentStage; }
        }

        public Stream ClientStream { get; set; }

        public Stream ServerStream { get; set; }

        public HttpRequestHeaders RequestHeaders { get; set; }

        public HttpResponseHeaders ResponseHeaders { get; set; }

        public void NextStage() {
            if (_currentStage == ProcessingStage.Finish) {
                return;
            }

            _currentStage++;
        }

        public void StopProcessing() {
            _currentStage = ProcessingStage.Finish;
        }

    }

}