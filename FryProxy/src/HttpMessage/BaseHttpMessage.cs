using System;
using System.IO;

using FryProxy.HttpHeaders;

namespace FryProxy.HttpMessage {

    public class BaseHttpMessage {

        private const String ChunkedTransferEncoding = "chunked";

        private readonly HttpHeaders.HttpHeaders _headers;

        private String _startLine;

        public BaseHttpMessage(String startLine = null, HttpHeaders.HttpHeaders headers = null) {
            _startLine = startLine ?? String.Empty;
            _headers = headers ?? new HttpHeaders.HttpHeaders();

            GeneralHeaders = new GeneralHeaders(_headers);
            EntityHeaders = new EntityHeaders(_headers);
        }

        public Boolean Chunked {
            get { return (GeneralHeaders.TransferEncoding ?? String.Empty).Contains(ChunkedTransferEncoding); }
        }

        public virtual String StartLine {
            get { return _startLine; }
            set { _startLine = value; }
        }

        public HttpHeaders.HttpHeaders Headers {
            get { return _headers; }
        }

        public GeneralHeaders GeneralHeaders { get; private set; }

        public EntityHeaders EntityHeaders { get; private set; }

        public Stream Body { get; set; }

    }

}