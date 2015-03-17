using System;
using System.Text;

namespace FryProxy.Headers {

    public class HttpMessageHeaders {

        private const String ChunkedTransferEncoding = "chunked";

        private HttpHeadersCollection _httpHeadersCollection;

        private String _startLine;

        public HttpMessageHeaders(String startLine = null) {
            _startLine = startLine ?? String.Empty;
            _httpHeadersCollection = new HttpHeadersCollection();
        }

        public Boolean Chunked {
            get { return (GeneralHeaders.TransferEncoding ?? String.Empty).Contains(ChunkedTransferEncoding); }
        }

        public virtual String StartLine {
            get { return _startLine; }
            set { _startLine = value; }
        }

        public HttpHeadersCollection HeadersCollection {
            get { return _httpHeadersCollection; }
            set { _httpHeadersCollection = value ?? new HttpHeadersCollection(); }
        }

        public GeneralHeadersFacade GeneralHeaders {
            get { return new GeneralHeadersFacade(HeadersCollection); }
        }

        public EntityHeadersFacade EntityHeaders {
            get { return new EntityHeadersFacade(HeadersCollection); }
        }
        
        public override String ToString()
        {
            var sb = new StringBuilder(_startLine);

            foreach (var header in HeadersCollection.Pairs)
            {
                sb.AppendLine(String.Format("\t{0}:{1}", header.Key, header.Value));
            }

            return sb.ToString();
        }

    }

}