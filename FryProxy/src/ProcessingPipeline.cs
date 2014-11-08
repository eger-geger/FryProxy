using System;
using System.Collections.Generic;
using System.Diagnostics.Contracts;

namespace FryProxy {

    internal class ProcessingPipeline {

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
            try {
                while (_currentStage <= ProcessingStage.Completed) {
                    try {
                        InvokeCurrentStageAction(context, true);
                        _currentStage++;
                    } catch {
                        if (_currentStage != ProcessingStage.Completed) {
                            _currentStage = ProcessingStage.Completed;
                            InvokeCurrentStageAction(context, false);
                        }

                        throw;
                    }
                }
            } finally {
                _currentStage = ProcessingStage.Completed;
            }
        }

        private void InvokeCurrentStageAction(ProcessingContext context, Boolean rethrow) {
            var action = _processingActions[_currentStage];

            if (action == null) {
                return;
            }

            try {
                action.Invoke(context);
            } catch {
                if (rethrow) {
                    throw;
                }
            }
        }

        public void Stop() {
            _currentStage = ProcessingStage.Completed;
        }

    }

}