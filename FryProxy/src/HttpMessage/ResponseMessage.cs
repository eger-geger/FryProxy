using System;
using System.Text.RegularExpressions;

using FryProxy.HttpHeaders;

namespace FryProxy.HttpMessage {

    public class ResponseMessage : HttpMessage {

        private static readonly Regex ResponseLineRegex = new Regex(
            @"HTTP/(?<version>\d\.\d)\s(?<status>\d{3})\s(?<reason>.*)", RegexOptions.Compiled
            );

        public ResponseMessage(String startLine = null, HttpHeaders.HttpHeaders headers = null) : base(startLine, headers) {
            StartLine = base.StartLine;
            ResponseHeaders = new ResponseHeaders(Headers);
        }

        public ResponseHeaders ResponseHeaders { get; private set; }

        /// <summary>
        ///     HTTP response status code
        /// </summary>
        public Int32 StatusCode { get; set; }

        /// <summary>
        ///     HTTP protocol version
        /// </summary>
        public String Version { get; set; }

        /// <summary>
        ///     HTTP respnse status message
        /// </summary>
        public String Reason { get; set; }

        /// <summary>
        ///     First line of HTTP response message
        /// </summary>
        /// <exception cref="ArgumentException">
        ///     If Status-Line is invalid
        /// </exception>
        public override sealed String StartLine {
            get { return String.Format("HTTP/{0} {1} {2}", Version, StatusCode, Reason); }

            set {
                var match = ResponseLineRegex.Match(value);

                if (!match.Success) {
                    throw new ArgumentException("Ivalid Response-Line", "value");
                }

                Reason = match.Groups["reason"].Value;
                Version = match.Groups["version"].Value;
                StatusCode = Int32.Parse(match.Groups["status"].Value);

                base.StartLine = value;
            }
        }

    }

}