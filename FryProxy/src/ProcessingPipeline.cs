using System;
using System.Collections.Generic;
using System.Diagnostics.Contracts;

namespace FryProxy {

    internal class ProcessingPipeline {
        private readonly IDictionary<ProcessingStage, Action<ProcessingContext>> _processingActions;

        public ProcessingPipeline(IDictionary<ProcessingStage, Action<ProcessingContext>> processingActions) {
            Contract.Requires<ArgumentNullException>(processingActions != null, "processingActions");

            _processingActions = processingActions;
        }

        public void Start(ProcessingContext context) {
            for (context.Stage = ProcessingStage.ReceiveRequest; context.Stage <= ProcessingStage.Completed; context.Stage++) {
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
                    context.Exception = ex;
                    context.Stage = ProcessingStage.Completed;
                }
            }
        }
    }

}