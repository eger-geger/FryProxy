using System;

namespace FryProxy.HttpHeaders {

    /// <summary>
    ///     HTTP response message headers
    /// </summary>
    public class ResponseHeaders {

        private const String HeaderAge = "Age";

        private const String HeaderEtag = "Etag";

        private const String HeaderVary = "Vary";

        private const String HeaderServer = "Server";

        private const String HeaderLocation = "Location";

        private const String HeaderRetryAfter = "Retry-After";

        private const String HeaderAcceptRanges = "Accept-Ranges";

        private const String HeaderWWWAuthenticate = "WWW-Authenticate";
        private const String HeaderProxyAuthenticate = "Proxy-Authenticate";

        private readonly FryProxy.HttpHeaders.HttpHeaders _headers;

        public ResponseHeaders(FryProxy.HttpHeaders.HttpHeaders headers) {
            _headers = headers;
        }

        public String Age {
            get { return _headers[HeaderAge]; }
            set { _headers[HeaderAge] = value; }
        }

        public String Etag {
            get { return _headers[HeaderEtag]; }
            set { _headers[HeaderEtag] = value; }
        }

        public String Vary {
            get { return _headers[HeaderVary]; }
            set { _headers[HeaderVary] = value; }
        }

        public String Server {
            get { return _headers[HeaderServer]; }
            set { _headers[HeaderServer] = value; }
        }

        public String Location {
            get { return _headers[HeaderLocation]; }
            set { _headers[HeaderLocation] = value; }
        }

        public String RetryAfter {
            get { return _headers[HeaderRetryAfter]; }
            set { _headers[HeaderRetryAfter] = value; }
        }

        public String AcceptRanges {
            get { return _headers[HeaderAcceptRanges]; }
            set { _headers[HeaderAcceptRanges] = value; }
        }

        public String WWWAuthenticate {
            get { return _headers[HeaderWWWAuthenticate]; }
            set { _headers[HeaderWWWAuthenticate] = value; }
        }

        public String ProxyAuthenticate {
            get { return _headers[HeaderProxyAuthenticate]; }
            set { _headers[HeaderProxyAuthenticate] = value; }
        }

    }

}