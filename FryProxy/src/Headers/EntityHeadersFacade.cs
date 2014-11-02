using System;

namespace FryProxy.Headers {

    public sealed class EntityHeadersFacade {

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

        private readonly HttpHeadersCollection HeadersCollection;

        public EntityHeadersFacade(HttpHeadersCollection headersCollection) {
            HeadersCollection = headersCollection;
        }

        public String Allow {
            get { return HeadersCollection[AllowHeader]; }
            set { HeadersCollection[AllowHeader] = value; }
        }

        public String Expires {
            get { return HeadersCollection[ExpiresHeader]; }
            set { HeadersCollection[ExpiresHeader] = value; }
        }

        public String LastModified {
            get { return HeadersCollection[LastModifiedHeader]; }
            set { HeadersCollection[LastModifiedHeader] = value; }
        }

        public String ContentMD5 {
            get { return HeadersCollection[ContentMD5Header]; }
            set { HeadersCollection[ContentMD5Header] = value; }
        }

        public String ContentType {
            get { return HeadersCollection[ContentTypeHeader]; }
            set { HeadersCollection[ContentTypeHeader] = value; }
        }

        public String ContentRange {
            get { return HeadersCollection[ContentRangeHeader]; }
            set { HeadersCollection[ContentRangeHeader] = value; }
        }

        public Int32? ContentLength {
            get {
                var contentLength = HeadersCollection[ContentLengthHeader];

                if (contentLength != null) {
                    return Int32.Parse(contentLength);
                }

                return null;
            }
            set { HeadersCollection[ContentLengthHeader] = value.HasValue ? value.Value.ToString() : null; }
        }

        public String ContentLanguage {
            get { return HeadersCollection[ContentLanguageHeader]; }
            set { HeadersCollection[ContentLanguageHeader] = value; }
        }

        public String ContentLocation {
            get { return HeadersCollection[ContentLocationHeader]; }
            set { HeadersCollection[ContentLocationHeader] = value; }
        }

        public String ContentEncoding {
            get { return HeadersCollection[ContentEncodingHeader]; }
            set { HeadersCollection[ContentEncodingHeader] = value; }
        }

    }

}