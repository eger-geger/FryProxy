using System;
using System.Diagnostics.Contracts;
using System.IO;
using System.Text;

using FryProxy.Headers;

namespace FryProxy.Utility {

    public static class HttpResponseExtensions {

        private const Int32 DefaultBufferSize = 2048;

        private static String CreateResponseLine(Int32 statusCode, String reason = null) {
            Contract.Requires<ArgumentOutOfRangeException>(statusCode > 99 && statusCode < 600, "statusCode");

            return String.Format("HTTP/1.1 {0} {1}", statusCode, reason ?? String.Empty);
        }

        public static void SendHttpResponse(
            this Stream stream, Int32 statusCode, String reason, Stream content = null, Int32 bufferSize = DefaultBufferSize) {
            Contract.Requires<ArgumentNullException>(stream != null, "stream");

            stream.SendHttpResponse(new HttpResponseHeaders(CreateResponseLine(statusCode, reason)), content, bufferSize);
        }

        public static void SendHttpResponse(
            this Stream stream, HttpResponseHeaders headers, Stream content = null, Int32 bufferSize = DefaultBufferSize) {
                Contract.Requires<ArgumentNullException>(stream != null, "stream");
            Contract.Requires<ArgumentNullException>(headers != null, "headers");

            stream.WriteHttpMessage(headers, content ?? Stream.Null, bufferSize);
        }

        public static void SendInternalServerError(this Stream stream, Stream content, Int32 bufferSize = DefaultBufferSize) {
            Contract.Requires<ArgumentNullException>(stream != null, "stream");

            SendHttpResponse(stream, 500, "Internal Server Error", content, bufferSize);
        }

        public static void SendInvalidRequest(this Stream stream, Stream content, Int32 bufferSize = DefaultBufferSize) {
            SendHttpResponse(stream, 400, "Invalid Request", content, bufferSize);
        }

        public static void SendConnectionEstablished(this Stream stream) {
            Contract.Requires<ArgumentNullException>(stream != null, "stream");

            var writer = new StreamWriter(stream, Encoding.ASCII);

            writer.WriteLine(CreateResponseLine(200, "Connection Established"));
            writer.WriteLine();
            writer.Flush();
        }

    }

}