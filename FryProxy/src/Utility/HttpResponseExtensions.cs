using System;
using System.Diagnostics.Contracts;
using System.IO;
using System.Text;

using FryProxy.Headers;

namespace FryProxy.Utility {

    /// <summary>
    ///     Provides methods for writing HTTP response messagess
    /// </summary>
    public static class HttpResponseExtensions {

        private const Int32 DefaultBufferSize = 2048;

        /// <summary>
        ///     Create formatted HTTP response line
        /// </summary>
        /// <param name="statusCode">response status code</param>
        /// <param name="reason">response status line message</param>
        /// <param name="httpVersion">HTTP protocol version.</param>
        /// <returns></returns>
        public static String CreateResponseLine(Int32 statusCode, String reason = null, String httpVersion = "1.1") {
            Contract.Requires<ArgumentOutOfRangeException>(statusCode > 99 && statusCode < 600, "statusCode");

            return String.Format("HTTP/{2} {0} {1}", statusCode, reason ?? String.Empty, httpVersion);
        }

        /// <summary>
        ///     Create HTTP response message and write it to stream
        /// </summary>
        /// <param name="stream">stream message will be written to</param>
        /// <param name="statusCode">response status code</param>
        /// <param name="reason">response status line message</param>
        /// <param name="content">stream to read message body from</param>
        /// <param name="bufferSize">size of buffer used for copying message body</param>
        public static void SendHttpResponse(
            this Stream stream, Int32 statusCode, String reason, Stream content = null, Int32 bufferSize = DefaultBufferSize) {
            Contract.Requires<ArgumentNullException>(stream != null, "stream");

            stream.SendHttpResponse(new HttpResponseHeaders(CreateResponseLine(statusCode, reason)), content, bufferSize);
        }

        /// <summary>
        ///     Create HTTP response message and write it to stream
        /// </summary>
        /// <param name="stream">stream message will be written to</param>
        /// <param name="headers">HTTP message header</param>
        /// <param name="content">stream to read message body from</param>
        /// <param name="bufferSize">size of buffer used for copying message body</param>
        public static void SendHttpResponse(
            this Stream stream, HttpResponseHeaders headers, Stream content = null, Int32 bufferSize = DefaultBufferSize) {
                Contract.Requires<ArgumentNullException>(stream != null, "stream");
                Contract.Requires<ArgumentNullException>(headers != null, "headers");

            stream.WriteHttpMessage(headers, content ?? Stream.Null, bufferSize);
        }

        /// <summary>
        ///     Write HTTP "Insternal Server Error" message with "500" status code 
        /// </summary>
        /// <param name="stream">stream message will be written to</param>
        /// <param name="content">stream to read message body from</param>
        /// <param name="bufferSize">size of buffer used for copying message body</param>
        public static void SendInternalServerError(this Stream stream, Stream content = null, Int32 bufferSize = DefaultBufferSize) {
            SendHttpResponse(stream, 500, "Internal Server Error", content, bufferSize);
        }

        /// <summary>
        ///     Write HTTP "Invalid Request" message with "400" status code
        /// </summary>
        /// <param name="stream">stream message will be written to</param>
        /// <param name="content">stream to read message body from</param>
        /// <param name="bufferSize">size of buffer used for copying message body</param>
        public static void SendInvalidRequest(this Stream stream, Stream content = null, Int32 bufferSize = DefaultBufferSize) {
            SendHttpResponse(stream, 400, "Invalid Request", content, bufferSize);
        }

        /// <summary>
        ///     Write HTTP "Connection Established" message with "200" status code
        /// </summary>
        /// <param name="stream">stream message will be written to</param>
        public static void SendConnectionEstablished(this Stream stream) {
            Contract.Requires<ArgumentNullException>(stream != null, "stream");

            var writer = new StreamWriter(stream, Encoding.ASCII);

            writer.WriteLine(CreateResponseLine(200, "Connection Established"));
            writer.WriteLine("Connection: close");
            writer.WriteLine();
            writer.Flush();
        }

    }

}