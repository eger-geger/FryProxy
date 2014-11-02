using System;

namespace FryProxy.Headers {

    /// <summary>
    ///     HTTP response message headers
    /// </summary>
    public class ResponseHeadersFacade {

        public const String AgeHeader = "Age";

        public const String EtagHeader = "Etag";

        public const String VaryHeader = "Vary";

        public const String ServerHeader = "Server";

        public const String LocationHeader = "Location";

        public const String RetryAfterHeader = "Retry-After";

        public const String AcceptRangesHeader = "Accept-Ranges";

        public const String WWWAuthenticateHeader = "WWW-Authenticate";
        public const String ProxyAuthenticateHeader = "Proxy-Authenticate";

        private readonly HttpHeadersCollection HeadersCollection;

        public ResponseHeadersFacade(HttpHeadersCollection headersCollection) {
            HeadersCollection = headersCollection;
        }

        public String Age {
            get { return HeadersCollection[AgeHeader]; }
            set { HeadersCollection[AgeHeader] = value; }
        }

        public String Etag {
            get { return HeadersCollection[EtagHeader]; }
            set { HeadersCollection[EtagHeader] = value; }
        }

        public String Vary {
            get { return HeadersCollection[VaryHeader]; }
            set { HeadersCollection[VaryHeader] = value; }
        }

        public String Server {
            get { return HeadersCollection[ServerHeader]; }
            set { HeadersCollection[ServerHeader] = value; }
        }

        public String Location {
            get { return HeadersCollection[LocationHeader]; }
            set { HeadersCollection[LocationHeader] = value; }
        }

        public String RetryAfter {
            get { return HeadersCollection[RetryAfterHeader]; }
            set { HeadersCollection[RetryAfterHeader] = value; }
        }

        public String AcceptRanges {
            get { return HeadersCollection[AcceptRangesHeader]; }
            set { HeadersCollection[AcceptRangesHeader] = value; }
        }

        public String WWWAuthenticate {
            get { return HeadersCollection[WWWAuthenticateHeader]; }
            set { HeadersCollection[WWWAuthenticateHeader] = value; }
        }

        public String ProxyAuthenticate {
            get { return HeadersCollection[ProxyAuthenticateHeader]; }
            set { HeadersCollection[ProxyAuthenticateHeader] = value; }
        }

    }

}