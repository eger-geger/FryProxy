using System;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Text;

namespace TrotiNet {

    /// <summary>
    ///     Wrapper around BaseProxyLogic that adds various utility functions
    /// </summary>
    public class ProxyLogic : BaseProxyLogic {

        /// <summary>
        ///     Instantiate a transparent proxy
        /// </summary>
        /// <param name="clientHttpSocket">Client browser-proxy socket</param>
        public ProxyLogic(HttpSocket clientHttpSocket) : base(clientHttpSocket) {}

        /// <summary>
        ///     Static constructor
        /// </summary>
        public static AbstractProxyLogic CreateProxy(HttpSocket socketBP) {
            return new ProxyLogic(socketBP);
        }

        /// <summary>
        ///     Change the request Path; also change the 'Host' request header,
        ///     when necessary
        /// </summary>
        /// <remarks>
        ///     If required, this function should be called from
        ///     <c>RequestHandler</c>.
        /// </remarks>
        public void ChangeRequestURI(string newURI) {
            if (RequestLine == null)
                throw new RuntimeException("Request line not available");

            RequestLine.Path = newURI;

            if (RequestHeaders != null && RequestHeaders.Host != null) {
                // Extract the host from the Path
                int prefix = newURI.IndexOf("://");
                string s = (prefix < 0)
                    ? newURI
                    : newURI.Substring(prefix + 3);

                int i = s.IndexOf("/");
                if (i <= 0)
                    // No host in Path
                    return;
                int j = s.IndexOf(":", 0, i);
                if (j >= 0)
                    // Ignore the port number
                    i = j;
                string host = s.Substring(0, i);

                // Update the 'Host' HTTP header
                RequestHeaders.Host = host;
            }
        }

        /// <summary>
        ///     Download the chunked file and return the byte array
        /// </summary>
        private byte[] GetChunkedContent() {
            char[] c_ChunkSizeEnd = {' ', ';'};
            var chunked_stream = new MemoryStream();

            // (RFC 2616, sections 3.6.1, 19.4.6)
            while (true) {
                string chunk_header = ServerSocket.ReadAsciiLine();

                if (string.IsNullOrEmpty(chunk_header))
                    continue;

                int sc = chunk_header.IndexOfAny(c_ChunkSizeEnd);
                string hexa_size;
                if (sc > -1)
                    // We have chunk extensions: ignore them
                    hexa_size = chunk_header.Substring(0, sc);
                else
                    hexa_size = chunk_header;

                uint size;
                try {
                    size = Convert.ToUInt32(hexa_size, 16);
                } catch {
                    string s = chunk_header.Length > 20
                        ? (chunk_header.Substring(0, 17) + "...")
                        : chunk_header;
                    throw new HttpProtocolBroken(
                        "Could not parse chunk size in: " + s);
                }

                if (size == 0)
                    break;

                var buffer = new byte[size];
                ServerSocket.TunnelDataTo(buffer, size);

                chunked_stream.Write(buffer, 0, (int) size);
            }

            return chunked_stream.ToArray();
        }

        /// <summary>
        ///     Get a file with a known file size (i.e., not chunked).
        /// </summary>
        private byte[] GetNonChunkedContent() {
            // Find out if there is a message body
            // (RFC 2616, section 4.4)
            int sc = ResponseStatusLine.StatusCode;
            if (RequestLine.Method.Equals("HEAD") ||
                sc == 204 || sc == 304 || (sc >= 100 && sc <= 199))
                return new byte[0];

            bool bResponseMessageChunked = false;
            uint ResponseMessageLength = 0;
            if (ResponseHeaders.TransferEncoding != null) {
                bResponseMessageChunked = Array.IndexOf(
                    ResponseHeaders.TransferEncoding, "chunked") >= 0;
                Debug.Assert(bResponseMessageChunked);
                if (bResponseMessageChunked) {
                    throw new RuntimeException(
                        "Chunked data found when not expected");
                }
            }

            if (ResponseHeaders.ContentLength != null) {
                ResponseMessageLength =
                    (uint) ResponseHeaders.ContentLength;

                if (ResponseMessageLength == 0)
                    return new byte[0];
            } else {
                // If the connection is not being closed,
                // we need a content length.
                Debug.Assert(
                    !State.bPersistConnectionPS);
            }

            var buffer = new byte[ResponseMessageLength];
            ServerSocket.TunnelDataTo(buffer, ResponseMessageLength);

            return buffer;
        }

        /// <summary>
        ///     If this method is called on a response, either the custom
        ///     response pipeline or the 302 redirect MUST be used.
        /// </summary>
        protected byte[] GetContent() {
            byte[] content = null;

            if (ResponseHeaders.TransferEncoding != null &&
                Array.IndexOf(ResponseHeaders.TransferEncoding,
                    "chunked") >= 0)
                content = GetChunkedContent();
            else
                content = GetNonChunkedContent();

            return (content ?? new byte[0]);
        }

        /// <summary>
        ///     Interpret a message with respect to its content encoding
        /// </summary>
        public Stream GetResponseMessageStream(byte[] msg) {
            Stream inS = new MemoryStream(msg);
            return GetResponseMessageStream(inS);
        }

        /// <summary>
        ///     Interpret a message with respect to its content encoding
        /// </summary>
        public Stream GetResponseMessageStream(Stream inS) {
            Stream outS = null;
            string ce = ResponseHeaders.ContentEncoding;
            if (!String.IsNullOrEmpty(ce)) {
                if (ce.StartsWith("deflate"))
                    outS = new DeflateStream(inS, CompressionMode.Decompress);
                else if (ce.StartsWith("gzip"))
                    outS = new GZipStream(inS, CompressionMode.Decompress);
                else if (!ce.StartsWith("identity")) {
                    throw new RuntimeException(
                        "Unsupported Content-Encoding '" + ce + "'");
                }
            }

            if (outS == null)
                return inS;
            return outS;
        }

        /// <summary>
        ///     Compress a byte array based on the content encoding header
        /// </summary>
        /// <param name="output">The content to be compressed</param>
        /// <returns>The compressed content</returns>
        public byte[] CompressResponse(byte[] output) {
            string ce = ResponseHeaders.ContentEncoding;
            if (!String.IsNullOrEmpty(ce)) {
                if (ce.StartsWith("deflate")) {
                    using (var ms = new MemoryStream()) {
                        using (var ds = new DeflateStream(ms,
                            CompressionMode.Compress, true)) {
                            ds.Write(output, 0, output.Length);
                            ds.Close();
                        }
                        return ms.ToArray();
                    }
                }
                if (ce.StartsWith("gzip")) {
                    using (var ms = new MemoryStream()) {
                        using (var gs = new GZipStream(ms,
                            CompressionMode.Compress, true)) {
                            gs.Write(output, 0, output.Length);
                            gs.Close();
                        }
                        return ms.ToArray();
                    }
                }
                if (!ce.StartsWith("identity")) {
                    throw new RuntimeException(
                        "Unsupported Content-Encoding '" + ce + "'");
                }
            }

            return output;
        }

        /// <summary>
        ///     Get an encoded byte array for a given string
        /// </summary>
        public byte[] EncodeStringResponse(string s, Encoding encoding) {
            byte[] output = encoding.GetBytes(s);
            return CompressResponse(output);
        }

    }

}
