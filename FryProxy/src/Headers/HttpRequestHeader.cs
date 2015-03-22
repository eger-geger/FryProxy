using System;
using System.Text;
using System.Text.RegularExpressions;

namespace FryProxy.Headers {

    public class HttpRequestHeader : HttpMessageHeader {

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

        /// <summary>
        ///     Convert generic <see cref="HttpMessageHeader"/> to <see cref="HttpResponseHeader"/>
        /// </summary>
        /// <param name="header">generic HTTP header</param>
        /// <returns>HTTP request message header</returns>
        public HttpRequestHeader(HttpMessageHeader header) : base(header.StartLine, header.Headers)
        {
            StartLine = header.StartLine;
        }

        public HttpRequestHeader(String startLine = null) : base(startLine) {
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
                .AppendLine(Headers.ToString())
                .ToString();
        }

        /// <summary>
        ///     Host header value
        /// </summary>
        public String Host {
            get { return Headers[HostHeader]; }
            set { Headers[HostHeader] = value; }
        }

        /// <summary>
        ///     Referer header value
        /// </summary>
        public String Referer {
            get { return Headers[RefererHeader]; }
            set { Headers[RefererHeader] = value; }
        }

        public String TE {
            get { return Headers[TEHeader]; }
            set { Headers[TEHeader] = value; }
        }

        public String Range {
            get { return Headers[RangeHeader]; }
            set { Headers[RangeHeader] = value; }
        }

        public String From {
            get { return Headers[FromHeader]; }
            set { Headers[FromHeader] = value; }
        }

        public String Expect {
            get { return Headers[ExpectHeader]; }
            set { Headers[ExpectHeader] = value; }
        }

        public String UserAgent {
            get { return Headers[UserAgentHeader]; }
            set { Headers[UserAgentHeader] = value; }
        }

        public String MaxForwards {
            get { return Headers[MaxForwardsHeader]; }
            set { Headers[MaxForwardsHeader] = value; }
        }

        public String Authorization {
            get { return Headers[AuthorizationHeader]; }
            set { Headers[AuthorizationHeader] = value; }
        }

        public String ProxyAuthorization {
            get { return Headers[ProxyAuthorizationHeader]; }
            set { Headers[ProxyAuthorizationHeader] = value; }
        }

        public String Accept {
            get { return Headers[AcceptHeader]; }
            set { Headers[AcceptHeader] = value; }
        }

        public String AcceptCharset {
            get { return Headers[AcceptCharsetHeader]; }
            set { Headers[AcceptCharsetHeader] = value; }
        }

        public String AcceptEncoding {
            get { return Headers[AcceptEncodingHeader]; }
            set { Headers[AcceptEncodingHeader] = value; }
        }

        public String AcceptLanguage {
            get { return Headers[AcceptLanguageHeader]; }
            set { Headers[AcceptLanguageHeader] = value; }
        }

        public String IfMatch {
            get { return Headers[IfMatchHeader]; }
            set { Headers[IfMatchHeader] = value; }
        }

        public String IfRange {
            get { return Headers[IfRangeHeader]; }
            set { Headers[IfRangeHeader] = value; }
        }

        public String IfNoneMatch {
            get { return Headers[IfNoneMatchHeader]; }
            set { Headers[IfNoneMatchHeader] = value; }
        }

        public String IfModifiedSince {
            get { return Headers[IfModifiedSinceHeader]; }
            set { Headers[IfModifiedSinceHeader] = value; }
        }

        public String IfUnmodifiedSince {
            get { return Headers[IfUnmodifiedSinceHeader]; }
            set { Headers[IfUnmodifiedSinceHeader] = value; }
        }

    }

}