using System;

namespace FryProxy.HttpHeaders {

    public class GeneralHeaders {

        public const String PragmaHeader = "Pragma";

        public const String ConnectionHeader = "Connection";

        public const String CacheControlHeader = "Cache-Control";

        public const String TransferEncodingHeader = "Transfer-Encoding";

        public const String TrailerHeader = "Trailer";

        public readonly HttpHeaders _headers;

        public GeneralHeaders(HttpHeaders headers) {
            _headers = headers;
        }

        /// <summary>
        ///     Cache-Control header value
        /// </summary>
        public String CacheControl {
            get { return _headers[CacheControlHeader]; }
            set { _headers[CacheControlHeader] = value; }
        }

        /// <summary>
        ///     Connection header value
        /// </summary>
        public String Connection {
            get { return _headers[ConnectionHeader]; }
            set { _headers[ConnectionHeader] = value; }
        }

        /// <summary>
        ///     Pragma header value
        /// </summary>
        public String Pragma {
            get { return _headers[PragmaHeader]; }
            set { _headers[PragmaHeader] = value; }
        }

        public String TransferEncoding {
            get { return _headers[TransferEncodingHeader]; }
            set { _headers[TransferEncodingHeader] = value; }
        }

        public String Trailer {
            get { return _headers[TrailerHeader]; }
            set { _headers[TrailerHeader] = value; }
        }

    }

}