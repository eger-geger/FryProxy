using System;
using System.Text.RegularExpressions;

namespace FryProxy.Headers {

    public class HttpRequestHeaders : HttpMessageHeaders {

        private static readonly Regex RequestLineRegex = new Regex(
            @"(?<method>\w+)\s(?<uri>.+)\sHTTP/(?<version>\d\.\d)", RegexOptions.Compiled
            );

        public HttpRequestHeaders(String startLine = null) : base(startLine) {
            StartLine = base.StartLine;
        }

        public RequestHeadersFacade RequestHeaders {
            get { return new RequestHeadersFacade(HeadersCollection); }
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

    }

}