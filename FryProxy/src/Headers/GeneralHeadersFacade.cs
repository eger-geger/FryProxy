using System;

namespace FryProxy.Headers {

    public class GeneralHeadersFacade {

        public const String PragmaHeader = "Pragma";

        public const String ConnectionHeader = "Connection";

        public const String CacheControlHeader = "Cache-Control";

        public const String TransferEncodingHeader = "Transfer-Encoding";

        public const String TrailerHeader = "Trailer";

        public readonly HttpHeadersCollection HeadersCollection;

        public GeneralHeadersFacade(HttpHeadersCollection headersCollection) {
            HeadersCollection = headersCollection;
        }

        /// <summary>
        ///     Cache-Control header value
        /// </summary>
        public String CacheControl {
            get { return HeadersCollection[CacheControlHeader]; }
            set { HeadersCollection[CacheControlHeader] = value; }
        }

        /// <summary>
        ///     Connection header value
        /// </summary>
        public String Connection {
            get { return HeadersCollection[ConnectionHeader]; }
            set { HeadersCollection[ConnectionHeader] = value; }
        }

        /// <summary>
        ///     Pragma header value
        /// </summary>
        public String Pragma {
            get { return HeadersCollection[PragmaHeader]; }
            set { HeadersCollection[PragmaHeader] = value; }
        }

        public String TransferEncoding {
            get { return HeadersCollection[TransferEncodingHeader]; }
            set { HeadersCollection[TransferEncodingHeader] = value; }
        }

        public String Trailer {
            get { return HeadersCollection[TrailerHeader]; }
            set { HeadersCollection[TrailerHeader] = value; }
        }

    }

}