using System;
using System.Diagnostics.Contracts;

using FryProxy.Headers;

namespace FryProxy.Handlers {

    public static class ConnectionHeaderHandler {

        public static void RemoveIfPresent(HttpMessageHeaders messageHeaders) {
            Contract.Requires<ArgumentNullException>(messageHeaders != null, "message");

            if (messageHeaders.HeadersCollection.Contains(GeneralHeadersFacade.ConnectionHeader)) {
                messageHeaders.HeadersCollection.RemoveAll(GeneralHeadersFacade.ConnectionHeader);
            }
        }

    }

}