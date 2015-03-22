using System;
using System.IO;
using FryProxy.Headers;

namespace FryProxy.Writers
{
    /// <summary>
    ///     Message writer with capability to write specific types of HTTP response
    /// </summary>
    public class HttpResponseWriter : HttpMessageWriter
    {
        private const String DefaultHttpVersion = "1.1";

        /// <summary>
        ///     Creates new writer instance, writing to provided stream
        /// </summary>
        /// <param name="outputStream">stream which will be written to</param>
        public HttpResponseWriter(Stream outputStream) : base(outputStream)
        {
        }

        /// <summary>
        ///     Write HTTP response message to underlying stream
        /// </summary>
        /// <param name="statusCode">response status code</param>
        /// <param name="reason">response status message</param>
        /// <param name="httpVersion">HTTP protocol version</param>
        /// <param name="body">response body</param>
        public void Write(Int32 statusCode, String reason = null, String httpVersion = null, Stream body = null)
        {
            Write(new HttpResponseHeader(statusCode, reason ?? String.Empty, httpVersion ?? DefaultHttpVersion), body);
        }

        /// <summary>
        ///     Write HTTP "Request Timeout" message to underlying stream
        /// </summary>
        public void WriteRequestTimeout()
        {
            Write(408, "Request Timeout");
        }

        /// <summary>
        ///     Write HTTP "Connection Established" message to given stream
        /// </summary>
        public void WriteConnectionEstablished()
        {
            Write(200, "Connection Established");
        }
    }
}