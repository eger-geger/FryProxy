using System;

namespace FryProxy.HttpHeaders {

    /// <summary>
    ///     HTTP request message headers
    /// </summary>
    public class RequestHeaders {

        public const String TEHeader = "TE";

        public const String RangeHeader = "Range";

        public const String FromHeader = "From";
        public const String HostHeader = "Host";
        public const String RefererHeader = "Referer";
        public const String ExpectHeader = "Expect";

        public const String UserAgentHeader = "User-Agent";

        public const String MaxForwardsHeader = "Max-Forwards";

        public const String AuthorizationHeader = "Authorization";
        public const String ProxyAuthorizationHeader = "Proxy-Authorization";

        public const String AcceptHeader = "Accept";
        public const String AcceptCharsetHeader = "Accept-Charset";
        public const String AcceptEncodingHeader = "Accept-Encoding";
        public const String AcceptLanguageHeader = "Accept-Language";

        public const String IfMatchHeader = "If-Match";
        public const String IfRangeHeader = "If-Range";
        public const String IfNoneMatchHeader = "If-None-Match";
        public const String IfModifiedSinceHeader = "If-Modified-Since";
        public const String IfUnmodifiedSinceHeader = "If-Unmodified-Since";

        private readonly HttpHeaders _headers;

        public RequestHeaders(HttpHeaders headers) {
            _headers = headers;
        }

        /// <summary>
        ///     Host header value
        /// </summary>
        public String Host {
            get { return _headers[HostHeader]; }
            set { _headers[HostHeader] = value; }
        }

        /// <summary>
        ///     Referer header value
        /// </summary>
        public String Referer {
            get { return _headers[RefererHeader]; }
            set { _headers[RefererHeader] = value; }
        }

        public String TE {
            get { return _headers[TEHeader]; }
            set { _headers[TEHeader] = value; }
        }

        public String Range {
            get { return _headers[RangeHeader]; }
            set { _headers[RangeHeader] = value; }
        }

        public String From {
            get { return _headers[FromHeader]; }
            set { _headers[FromHeader] = value; }
        }

        public String Expect {
            get { return _headers[ExpectHeader]; }
            set { _headers[ExpectHeader] = value; }
        }

        public String UserAgent {
            get { return _headers[UserAgentHeader]; }
            set { _headers[UserAgentHeader] = value; }
        }

        public String MaxForwards {
            get { return _headers[MaxForwardsHeader]; }
            set { _headers[MaxForwardsHeader] = value; }
        }

        public String Authorization {
            get { return _headers[AuthorizationHeader]; }
            set { _headers[AuthorizationHeader] = value; }
        }

        public String ProxyAuthorization {
            get { return _headers[ProxyAuthorizationHeader]; }
            set { _headers[ProxyAuthorizationHeader] = value; }
        }

        public String Accept {
            get { return _headers[AcceptHeader]; }
            set { _headers[AcceptHeader] = value; }
        }

        public String AcceptCharset {
            get { return _headers[AcceptCharsetHeader]; }
            set { _headers[AcceptCharsetHeader] = value; }
        }

        public String AcceptEncoding {
            get { return _headers[AcceptEncodingHeader]; }
            set { _headers[AcceptEncodingHeader] = value; }
        }

        public String AcceptLanguage {
            get { return _headers[AcceptLanguageHeader]; }
            set { _headers[AcceptLanguageHeader] = value; }
        }

        public String IfMatch {
            get { return _headers[IfMatchHeader]; }
            set { _headers[IfMatchHeader] = value; }
        }

        public String IfRange {
            get { return _headers[IfRangeHeader]; }
            set { _headers[IfRangeHeader] = value; }
        }

        public String IfNoneMatch {
            get { return _headers[IfNoneMatchHeader]; }
            set { _headers[IfNoneMatchHeader] = value; }
        }

        public String IfModifiedSince {
            get { return _headers[IfModifiedSinceHeader]; }
            set { _headers[IfModifiedSinceHeader] = value; }
        }

        public String IfUnmodifiedSince {
            get { return _headers[IfUnmodifiedSinceHeader]; }
            set { _headers[IfUnmodifiedSinceHeader] = value; }
        }

    }

}