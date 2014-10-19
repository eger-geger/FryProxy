using System;

namespace FryProxy.HttpHeaders {

    /// <summary>
    ///     HTTP response message headers
    /// </summary>
    public class ResponseHeaders {

        public const String AgeHeader = "Age";

        public const String EtagHeader = "Etag";

        public const String VaryHeader = "Vary";

        public const String ServerHeader = "Server";

        public const String LocationHeader = "Location";

        public const String RetryAfterHeader = "Retry-After";

        public const String AcceptRangesHeader = "Accept-Ranges";

        public const String WWWAuthenticateHeader = "WWW-Authenticate";
        public const String ProxyAuthenticateHeader = "Proxy-Authenticate";

        private readonly HttpHeaders _headers;

        public ResponseHeaders(HttpHeaders headers) {
            _headers = headers;
        }

        public String Age {
            get { return _headers[AgeHeader]; }
            set { _headers[AgeHeader] = value; }
        }

        public String Etag {
            get { return _headers[EtagHeader]; }
            set { _headers[EtagHeader] = value; }
        }

        public String Vary {
            get { return _headers[VaryHeader]; }
            set { _headers[VaryHeader] = value; }
        }

        public String Server {
            get { return _headers[ServerHeader]; }
            set { _headers[ServerHeader] = value; }
        }

        public String Location {
            get { return _headers[LocationHeader]; }
            set { _headers[LocationHeader] = value; }
        }

        public String RetryAfter {
            get { return _headers[RetryAfterHeader]; }
            set { _headers[RetryAfterHeader] = value; }
        }

        public String AcceptRanges {
            get { return _headers[AcceptRangesHeader]; }
            set { _headers[AcceptRangesHeader] = value; }
        }

        public String WWWAuthenticate {
            get { return _headers[WWWAuthenticateHeader]; }
            set { _headers[WWWAuthenticateHeader] = value; }
        }

        public String ProxyAuthenticate {
            get { return _headers[ProxyAuthenticateHeader]; }
            set { _headers[ProxyAuthenticateHeader] = value; }
        }

    }

}