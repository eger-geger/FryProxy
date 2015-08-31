using System;

namespace FryProxy.Headers {

    public class GeneralHeaders {

        public const String PragmaHeader = "Pragma";

        public const String ConnectionHeader = "Connection";

        public const String ProxyConnectionHeader = "Proxy-Connection";

        public const String CacheControlHeader = "Cache-Control";

        public const String TransferEncodingHeader = "Transfer-Encoding";

        public const String TrailerHeader = "Trailer";

        public readonly HttpHeaders Headers;

        public GeneralHeaders(HttpHeaders headers) {
            Headers = headers;
        }

        /// <summary>
        ///     Cache-Control header value
        /// </summary>
        public String CacheControl {
            get { return Headers[CacheControlHeader]; }
            set { Headers[CacheControlHeader] = value; }
        }

        /// <summary>
        ///     Connection header value
        /// </summary>
        public String Connection {
            get { return Headers[ConnectionHeader]; }
            set { Headers[ConnectionHeader] = value; }
        }

        public String ProxyConnection
        {
            get { return Headers[ProxyConnectionHeader]; }
            set { Headers[ProxyConnectionHeader] = value; }
        }

        /// <summary>
        ///     Pragma header value
        /// </summary>
        public String Pragma {
            get { return Headers[PragmaHeader]; }
            set { Headers[PragmaHeader] = value; }
        }

        public String TransferEncoding {
            get { return Headers[TransferEncodingHeader]; }
            set { Headers[TransferEncodingHeader] = value; }
        }

        public String Trailer {
            get { return Headers[TrailerHeader]; }
            set { Headers[TrailerHeader] = value; }
        }

    }

}