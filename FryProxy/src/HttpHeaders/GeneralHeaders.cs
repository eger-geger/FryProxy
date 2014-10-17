using System;

namespace FryProxy.HttpHeaders {

    public class GeneralHeaders {

        private const String HeaderPragma = "Pragma";

        private const String HeaderConnection = "Connection";

        private const String HeaderCacheControl = "Cache-Control";

        private const String HeaderTransferEncoding = "Transfer-Encoding";

        private const String HeaderTrailer = "Trailer";

        private readonly HttpHeaders _headers;

        public GeneralHeaders(HttpHeaders headers) {
            _headers = headers;
        }

        /// <summary>
        ///     Cache-Control header value
        /// </summary>
        public String CacheControl {
            get { return _headers[HeaderCacheControl]; }
            set { _headers[HeaderCacheControl] = value; }
        }

        /// <summary>
        ///     Connection header value
        /// </summary>
        public String Connection {
            get { return _headers[HeaderConnection]; }
            set { _headers[HeaderConnection] = value; }
        }

        /// <summary>
        ///     Pragma header value
        /// </summary>
        public String Pragma {
            get { return _headers[HeaderPragma]; }
            set { _headers[HeaderPragma] = value; }
        }

        public String TransferEncoding {
            get { return _headers[HeaderTransferEncoding]; }
            set { _headers[HeaderTransferEncoding] = value; }
        }

        public String Trailer {
            get { return _headers[HeaderTrailer]; }
            set { _headers[HeaderTrailer] = value; }
        }

    }

}