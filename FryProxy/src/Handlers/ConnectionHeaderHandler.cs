using System;
using System.Diagnostics.Contracts;

using FryProxy.HttpHeaders;
using FryProxy.HttpMessage;

namespace FryProxy.Handlers {

    public static class ConnectionHeaderHandler {

        public static void RemoveIfPresent(BaseHttpMessage message) {
            Contract.Requires<ArgumentNullException>(message != null, "message");

            if (message.Headers.Contains(GeneralHeaders.ConnectionHeader)) {
                message.Headers.RemoveAll(GeneralHeaders.ConnectionHeader);
            }
        }

    }

}