using System;
using System.Text;
using System.Text.RegularExpressions;

namespace FryProxy.Headers {

    public class HttpRequestHeaders : HttpMessageHeaders {

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

        private static readonly Regex RequestLineRegex = new Regex(
            @"(?<method>\w+)\s(?<uri>.+)\sHTTP/(?<version>\d\.\d)", RegexOptions.Compiled
            );

        public HttpRequestHeaders(String startLine = null) : base(startLine) {
            StartLine = base.StartLine;
        }

        public RequestMethodTypes MethodType {
            get {
                RequestMethodTypes methodType;

                var rawHttpMethod = Method;

                if (RequestMethodTypes.TryParse(Method, false, out methodType)) {
                    return methodType;
                }

                throw new InvalidOperationException(String.Format("Unknown method type: [{0}]", rawHttpMethod));
            }

            set {
                Method = Enum.GetName(typeof(RequestMethodTypes), value);
            }
        }

        /// <summary>
        ///     Request method
        /// </summary>
        public String Method { get; set; }

        /// <summary>
        ///     Request path
        /// </summary>
        public String RequestURI { get; set; }

        /// <summary>
        ///     HTTP protocol version
        /// </summary>
        public String Version { get; set; }

        /// <summary>
        ///     First line of HTTP response message
        /// </summary>
        /// <exception cref="ArgumentException">
        ///     If Request-Line is invalid
        /// </exception>
        public override sealed String StartLine {
            get { return String.Format("{0} {1} HTTP/{2}", Method, RequestURI, Version); }

            set {
                var match = RequestLineRegex.Match(value);

                if (!match.Success) {
                    throw new ArgumentException("Ivalid Response-Line", "value");
                }

                RequestURI = match.Groups["uri"].Value;
                Method = match.Groups["method"].Value;
                Version = match.Groups["version"].Value;

                base.StartLine = value;
            }

        }

        public override string ToString() {
            return new StringBuilder()
                .AppendLine(StartLine)
                .AppendLine(HeadersCollection.ToString())
                .ToString();
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