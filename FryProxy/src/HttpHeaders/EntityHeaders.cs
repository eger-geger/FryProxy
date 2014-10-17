using System;

namespace FryProxy.HttpHeaders {

    public sealed class EntityHeaders {

        private const String HeaderAllow = "Allow";

        private const String HeaderExpires = "Expires";

        private const String HeaderLastModified = "Last-Modified";

        private const String HeaderContentMD5 = "Content-MD5";
        private const String HeaderContentType = "Content-Type";
        private const String HeaderContentRange = "Content-Range";
        private const String HeaderContentLength = "Content-Length";
        private const String HeaderContentLanguage = "Content-Language";
        private const String HeaderContentLocation = "Content-Location";
        private const String HeaderContentEncoding = "Content-Encoding";

        private readonly HttpHeaders _headers;

        public EntityHeaders(HttpHeaders headers) {
            _headers = headers;
        }

        public String Allow {
            get { return _headers[HeaderAllow]; }
            set { _headers[HeaderAllow] = value; }
        }

        public String Expires {
            get { return _headers[HeaderExpires]; }
            set { _headers[HeaderExpires] = value; }
        }

        public String LastModified {
            get { return _headers[HeaderLastModified]; }
            set { _headers[HeaderLastModified] = value; }
        }

        public String ContentMD5 {
            get { return _headers[HeaderContentMD5]; }
            set { _headers[HeaderContentMD5] = value; }
        }

        public String ContentType {
            get { return _headers[HeaderContentType]; }
            set { _headers[HeaderContentType] = value; }
        }

        public String ContentRange {
            get { return _headers[HeaderContentRange]; }
            set { _headers[HeaderContentRange] = value; }
        }

        public Int32? ContentLength {
            get {
                var contentLength = _headers[HeaderContentLength];

                if (contentLength != null) {
                    return Int32.Parse(contentLength);
                }

                return null;
            }
            set { _headers[HeaderContentLength] = value.HasValue ? value.Value.ToString() : null; }
        }

        public String ContentLanguage {
            get { return _headers[HeaderContentLanguage]; }
            set { _headers[HeaderContentLanguage] = value; }
        }

        public String ContentLocation {
            get { return _headers[HeaderContentLocation]; }
            set { _headers[HeaderContentLocation] = value; }
        }

        public String ContentEncoding {
            get { return _headers[HeaderContentEncoding]; }
            set { _headers[HeaderContentEncoding] = value; }
        }

    }

}