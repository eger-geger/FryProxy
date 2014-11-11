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
            while (_currentStage < ProcessingStage.Completed) {
                InvokeCurrentStageAction(context);
                
                _currentStage = context.Exception == null 
                    ? _currentStage + 1 
                    : ProcessingStage.Completed;
            }

            InvokeCurrentStageAction(context);
        }

        private void InvokeCurrentStageAction(ProcessingContext context) {
            var action = _processingActions[_currentStage];

            if (action == null) {
                return;
            }

            try {
                action.Invoke(context);
            } catch (Exception ex) {
                context.Exception = ex;
            }
        }

        public void Stop() {
            _currentStage = ProcessingStage.Completed;
        }

    }

}