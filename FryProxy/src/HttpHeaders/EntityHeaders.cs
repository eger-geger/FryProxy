using System;

namespace FryProxy.HttpHeaders {

    public sealed class EntityHeaders {

        public const String AllowHeader = "Allow";

        public const String ExpiresHeader = "Expires";

        public const String LastModifiedHeader = "Last-Modified";

        public const String ContentMD5Header = "Content-MD5";
        public const String ContentTypeHeader = "Content-Type";
        public const String ContentRangeHeader = "Content-Range";
        public const String ContentLengthHeader = "Content-Length";
        public const String ContentLanguageHeader = "Content-Language";
        public const String ContentLocationHeader = "Content-Location";
        public const String ContentEncodingHeader = "Content-Encoding";

        private readonly HttpHeaders _headers;

        public EntityHeaders(HttpHeaders headers) {
            _headers = headers;
        }

        public String Allow {
            get { return _headers[AllowHeader]; }
            set { _headers[AllowHeader] = value; }
        }

        public String Expires {
            get { return _headers[ExpiresHeader]; }
            set { _headers[ExpiresHeader] = value; }
        }

        public String LastModified {
            get { return _headers[LastModifiedHeader]; }
            set { _headers[LastModifiedHeader] = value; }
        }

        public String ContentMD5 {
            get { return _headers[ContentMD5Header]; }
            set { _headers[ContentMD5Header] = value; }
        }

        public String ContentType {
            get { return _headers[ContentTypeHeader]; }
            set { _headers[ContentTypeHeader] = value; }
        }

        public String ContentRange {
            get { return _headers[ContentRangeHeader]; }
            set { _headers[ContentRangeHeader] = value; }
        }

        public Int32? ContentLength {
            get {
                var contentLength = _headers[ContentLengthHeader];

                if (contentLength != null) {
                    return Int32.Parse(contentLength);
                }

                return null;
            }
            set { _headers[ContentLengthHeader] = value.HasValue ? value.Value.ToString() : null; }
        }

        public String ContentLanguage {
            get { return _headers[ContentLanguageHeader]; }
            set { _headers[ContentLanguageHeader] = value; }
        }

        public String ContentLocation {
            get { return _headers[ContentLocationHeader]; }
            set { _headers[ContentLocationHeader] = value; }
        }

        public String ContentEncoding {
            get { return _headers[ContentEncodingHeader]; }
            set { _headers[ContentEncodingHeader] = value; }
        }

    }

}