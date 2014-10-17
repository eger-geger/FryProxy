using System;

namespace FryProxy.HttpHeaders {

    /// <summary>
    ///     HTTP request message headers
    /// </summary>
    public class RequestHeaders {

        private const String HeaderTE = "TE";

        private const String HeaderRange = "Range";

        private const String HeaderFrom = "From";
        private const String HeaderHost = "Host";
        private const String HeaderReferer = "Referer";
        private const String HeaderExpect = "Expect";

        private const String HeaderUserAgent = "User-Agent";

        private const String HeaderMaxForwards = "Max-Forwards";

        private const String HeaderAuthorization = "Authorization";
        private const String HeaderProxyAuthorization = "Proxy-Authorization";

        private const String HeaderAccept = "Accept";
        private const String HeaderAcceptCharset = "Accept-Charset";
        private const String HeaderAcceptEncoding = "Accept-Encoding";
        private const String HeaderAcceptLanguage = "Accept-Language";

        private const String HeaderIfMatch = "If-Match";
        private const String HeaderIfRange = "If-Range";
        private const String HeaderIfNoneMatch = "If-None-Match";
        private const String HeaderIfModifiedSince = "If-Modified-Since";
        private const String HeaderIfUnmodifiedSince = "If-Unmodified-Since";

        private readonly FryProxy.HttpHeaders.HttpHeaders _headers;

        public RequestHeaders(FryProxy.HttpHeaders.HttpHeaders headers) {
            _headers = headers;
        }

        /// <summary>
        ///     Host header value
        /// </summary>
        public String Host {
            get { return _headers[HeaderHost]; }
            set { _headers[HeaderHost] = value; }
        }

        /// <summary>
        ///     Referer header value
        /// </summary>
        public String Referer {
            get { return _headers[HeaderReferer]; }
            set { _headers[HeaderReferer] = value; }
        }

        public String TE {
            get { return _headers[HeaderTE]; }
            set { _headers[HeaderTE] = value; }
        }

        public String Range {
            get { return _headers[HeaderRange]; }
            set { _headers[HeaderRange] = value; }
        }

        public String From {
            get { return _headers[HeaderFrom]; }
            set { _headers[HeaderFrom] = value; }
        }

        public String Expect {
            get { return _headers[HeaderExpect]; }
            set { _headers[HeaderExpect] = value; }
        }

        public String UserAgent {
            get { return _headers[HeaderUserAgent]; }
            set { _headers[HeaderUserAgent] = value; }
        }

        public String MaxForwards {
            get { return _headers[HeaderMaxForwards]; }
            set { _headers[HeaderMaxForwards] = value; }
        }

        public String Authorization {
            get { return _headers[HeaderAuthorization]; }
            set { _headers[HeaderAuthorization] = value; }
        }

        public String ProxyAuthorization {
            get { return _headers[HeaderProxyAuthorization]; }
            set { _headers[HeaderProxyAuthorization] = value; }
        }

        public String Accept {
            get { return _headers[HeaderAccept]; }
            set { _headers[HeaderAccept] = value; }
        }

        public String AcceptCharset {
            get { return _headers[HeaderAcceptCharset]; }
            set { _headers[HeaderAcceptCharset] = value; }
        }

        public String AcceptEncoding {
            get { return _headers[HeaderAcceptEncoding]; }
            set { _headers[HeaderAcceptEncoding] = value; }
        }

        public String AcceptLanguage {
            get { return _headers[HeaderAcceptLanguage]; }
            set { _headers[HeaderAcceptLanguage] = value; }
        }

        public String IfMatch {
            get { return _headers[HeaderIfMatch]; }
            set { _headers[HeaderIfMatch] = value; }
        }

        public String IfRange {
            get { return _headers[HeaderIfRange]; }
            set { _headers[HeaderIfRange] = value; }
        }

        public String IfNoneMatch {
            get { return _headers[HeaderIfNoneMatch]; }
            set { _headers[HeaderIfNoneMatch] = value; }
        }

        public String IfModifiedSince {
            get { return _headers[HeaderIfModifiedSince]; }
            set { _headers[HeaderIfModifiedSince] = value; }
        }

        public String IfUnmodifiedSince {
            get { return _headers[HeaderIfUnmodifiedSince]; }
            set { _headers[HeaderIfUnmodifiedSince] = value; }
        }

    }

}