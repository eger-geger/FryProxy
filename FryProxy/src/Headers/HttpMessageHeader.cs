using System;
using System.Diagnostics.Contracts;
using System.Text;

namespace FryProxy.Headers {

    public class HttpMessageHeader {

        private const String ChunkedTransferEncoding = "chunked";

        private HttpHeaders _httpHeaders;

        private String _startLine;

        public HttpMessageHeader(String startLine, HttpHeaders headers)
        {
            Contract.Requires<ArgumentNullException>(!String.IsNullOrEmpty(startLine), "startLine");
            Contract.Requires<ArgumentNullException>(headers != null, "headers");

            _startLine = startLine;
            _httpHeaders = headers;
        }

        public HttpMessageHeader(String startLine = null) {
            _startLine = startLine ?? String.Empty;
            _httpHeaders = new HttpHeaders();
        }

        public Boolean Chunked {
            get { return (GeneralHeaders.TransferEncoding ?? String.Empty).Contains(ChunkedTransferEncoding); }
        }

        public virtual String StartLine {
            get { return _startLine; }
            set { _startLine = value; }
        }

        public HttpHeaders Headers {
            get { return _httpHeaders; }
            set { _httpHeaders = value ?? new HttpHeaders(); }
        }

        public GeneralHeaders GeneralHeaders {
            get { return new GeneralHeaders(Headers); }
        }

        public EntityHeaders EntityHeaders {
            get { return new EntityHeaders(Headers); }
        }
        
        public override String ToString()
        {
            var sb = new StringBuilder(_startLine).AppendLine();

            foreach (var headerLine in Headers.Lines)
            {
                sb.AppendLine(headerLine);
            }

            return sb.ToString();
        }

    }

}