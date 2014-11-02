using System;
using System.Diagnostics.Contracts;

using FryProxy.Utility;

namespace FryProxy.Headers {

    public static class HttpRequestHeadersExtensions {

        public static Boolean IsRequestMethod(this HttpRequestHeaders headers, RequestMethods method) {
            Contract.Requires<ArgumentNullException>(headers != null, "headers");

            return headers.Method.Equals(Enum.GetName(typeof(RequestMethods), method), StringComparison.OrdinalIgnoreCase);
        }

    }

}