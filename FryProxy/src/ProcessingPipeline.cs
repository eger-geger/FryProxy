using System;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.Net.Sockets;

namespace FryProxy {

    internal class ProcessingPipeline {

        private static readonly IList<SocketError> IgnoredSocketErrors = new[] {
            SocketError.ConnectionAborted,
            SocketError.ConnectionReset,
            SocketError.Disconnecting,
            SocketError.TimedOut
        };

        private readonly IDictionary<ProcessingStage, Action<ProcessingContext>> _processingActions;

        public ProcessingPipeline(IDictionary<ProcessingStage, Action<ProcessingContext>> processingActions) {
            Contract.Requires<ArgumentNullException>(processingActions != null, "processingActions");

            _processingActions = processingActions;
        }

        public void Start(ProcessingContext context) {
            for (; context.Stage <= ProcessingStage.Completed; context.Stage++) {
                if (!_processingActions.ContainsKey(context.Stage)) {
                    continue;
                }

                var action = _processingActions[context.Stage];

                if (action == null) {
                    continue;
                }

                try {
                    action.Invoke(context);
                } catch (Exception ex) {
                    if (!IsIgnoredSocketException(ex)) {
                        context.Exception = ex;
                    }

                    context.Stage = ProcessingStage.Completed;
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
    }

}