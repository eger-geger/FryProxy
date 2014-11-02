using System;

namespace FryProxy.Headers {

    /// <summary>
    ///     HTTP request message headers
    /// </summary>
    public class RequestHeadersFacade {

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

        private readonly HttpHeadersCollection HeadersCollection;

        public RequestHeadersFacade(HttpHeadersCollection headersCollection) {
            HeadersCollection = headersCollection;
        }

        /// <summary>
        ///     Host header value
        /// </summary>
        public String Host {
            get { return HeadersCollection[HostHeader]; }
            set { HeadersCollection[HostHeader] = value; }
        }

        /// <summary>
        ///     Referer header value
        /// </summary>
        public String Referer {
            get { return HeadersCollection[RefererHeader]; }
            set { HeadersCollection[RefererHeader] = value; }
        }

        public String TE {
            get { return HeadersCollection[TEHeader]; }
            set { HeadersCollection[TEHeader] = value; }
        }

        public String Range {
            get { return HeadersCollection[RangeHeader]; }
            set { HeadersCollection[RangeHeader] = value; }
        }

        public String From {
            get { return HeadersCollection[FromHeader]; }
            set { HeadersCollection[FromHeader] = value; }
        }

        public String Expect {
            get { return HeadersCollection[ExpectHeader]; }
            set { HeadersCollection[ExpectHeader] = value; }
        }

        public String UserAgent {
            get { return HeadersCollection[UserAgentHeader]; }
            set { HeadersCollection[UserAgentHeader] = value; }
        }

        public String MaxForwards {
            get { return HeadersCollection[MaxForwardsHeader]; }
            set { HeadersCollection[MaxForwardsHeader] = value; }
        }

        public String Authorization {
            get { return HeadersCollection[AuthorizationHeader]; }
            set { HeadersCollection[AuthorizationHeader] = value; }
        }

        public String ProxyAuthorization {
            get { return HeadersCollection[ProxyAuthorizationHeader]; }
            set { HeadersCollection[ProxyAuthorizationHeader] = value; }
        }

        public String Accept {
            get { return HeadersCollection[AcceptHeader]; }
            set { HeadersCollection[AcceptHeader] = value; }
        }

        public String AcceptCharset {
            get { return HeadersCollection[AcceptCharsetHeader]; }
            set { HeadersCollection[AcceptCharsetHeader] = value; }
        }

        public String AcceptEncoding {
            get { return HeadersCollection[AcceptEncodingHeader]; }
            set { HeadersCollection[AcceptEncodingHeader] = value; }
        }

        public String AcceptLanguage {
            get { return HeadersCollection[AcceptLanguageHeader]; }
            set { HeadersCollection[AcceptLanguageHeader] = value; }
        }

        public String IfMatch {
            get { return HeadersCollection[IfMatchHeader]; }
            set { HeadersCollection[IfMatchHeader] = value; }
        }

        public String IfRange {
            get { return HeadersCollection[IfRangeHeader]; }
            set { HeadersCollection[IfRangeHeader] = value; }
        }

        public String IfNoneMatch {
            get { return HeadersCollection[IfNoneMatchHeader]; }
            set { HeadersCollection[IfNoneMatchHeader] = value; }
        }

        public String IfModifiedSince {
            get { return HeadersCollection[IfModifiedSinceHeader]; }
            set { HeadersCollection[IfModifiedSinceHeader] = value; }
        }

        public String IfUnmodifiedSince {
            get { return HeadersCollection[IfUnmodifiedSinceHeader]; }
            set { HeadersCollection[IfUnmodifiedSinceHeader] = value; }
        }

    }

}