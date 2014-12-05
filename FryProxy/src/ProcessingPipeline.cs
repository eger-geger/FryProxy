using System;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.Net.Sockets;

using log4net;

namespace FryProxy {

    internal class ProcessingPipeline {

        private static readonly IList<SocketError> IgnoredSocketErrors = new[] {
            SocketError.ConnectionAborted, 
            SocketError.ConnectionReset,
            SocketError.Disconnecting,
            SocketError.TimedOut,
            
        }; 

        private readonly IDictionary<ProcessingStage, Action<ProcessingContext>> _processingActions;

        private ProcessingStage _currentStage;

        public ProcessingPipeline(IDictionary<ProcessingStage, Action<ProcessingContext>> processingActions) {
            Contract.Requires<ArgumentNullException>(processingActions != null, "processingActions");

            _processingActions = processingActions;
            _currentStage = ProcessingStage.ReceiveRequest;
        }

        public ProcessingStage CurrentStage {
            get { return _currentStage; }
        }

        public void Start(ProcessingContext context) {
            for (; _currentStage <= ProcessingStage.Completed; _currentStage++) {
                if (!_processingActions.ContainsKey(_currentStage)) {
                    continue;
                }

                var action = _processingActions[_currentStage];

                if (action == null) {
                    continue;
                }

                try {
                    action.Invoke(context);
                } catch (Exception ex) {
                    if (!IsIgnoredSocketException(ex)) {
                        context.Exception = ex;
                    }

                    _currentStage = ProcessingStage.Completed;
                }
            }
        }

        private static Boolean IsIgnoredSocketException(Exception exception) {
            while (exception != null) {
                if (exception is SocketException) {
                    return IgnoredSocketErrors.Contains((exception as SocketException).SocketErrorCode);
                }

                exception = exception.InnerException;
            }

            return false;
        }

        public void Stop() {
            _currentStage = ProcessingStage.Completed;
        }

    }

}