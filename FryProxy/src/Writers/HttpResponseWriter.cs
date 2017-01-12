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

        protected override bool WriteBody(HttpMessageHeader header, Stream body, Int64 bodyLength)
        {
            if (!IsRedirect(header as HttpResponseHeader))
            {
                return base.WriteBody(header, body, bodyLength);
            }
            else
            {
                if (Logger.IsDebugEnabled)
                {
                    Logger.Debug("Skipping redirect response body");       
                }
                return false;
            }
        }

        private static Boolean IsRedirect(HttpResponseHeader header)
        {
            return header != null 
                && (header.StatusCode >= 300 || header.StatusCode < 400) 
                && !String.IsNullOrEmpty(header.Location);
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
        ///     Write HTTP "Bad Gateway" message to underlying stream
        /// </summary>
        public void WriteBadGateway()
        {
            Write(502, "Bad Gateway");
        }

        /// <summary>
        ///     Write HTTP "Gateway Timeout" message to underlying stream
        /// </summary>
        public void WriteGatewayTimeout()
        {
            Write(504, "Gateway Timeout");
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