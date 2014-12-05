using System;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.Net.Sockets;

using log4net;

namespace FryProxy {

    internal class ProcessingPipeline {

        private readonly IDictionary<ProcessingStage, Action<ProcessingContext>> _processingActions;

        private ProcessingStage _currentStage;

        private static readonly ILog Logger = LogManager.GetLogger(typeof(ProcessingPipeline));

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
                    if (!IsConnectionAborted(ex.InnerException)) {
                        context.Exception = ex;
                    }

                    _currentStage = ProcessingStage.Completed;
                }
            }
        }

        private Boolean IsConnectionAborted(Exception exception) {
            var socketEx = exception as SocketException;

            if (socketEx == null) {
                return false;
            }

            Logger.ErrorFormat("Socket Exception: socket error code - [{0}]; native error code - [{1}]", socketEx.SocketErrorCode, socketEx.NativeErrorCode);

            return socketEx.SocketErrorCode == SocketError.ConnectionAborted || socketEx.SocketErrorCode == SocketError.TimedOut;
        }

        public void Stop() {
            _currentStage = ProcessingStage.Completed;
        }

    }

}