using System;
using System.IO;
using System.Text;

namespace FryProxy.HttpMessage {

    /// <summary>
    ///     Creates HTTP messages
    /// </summary>
    public static class ResponseMessageFactory {

        private const String ContentTypeUtf8PlainText = "text/plain; charset=UTF-8";

        /// <summary>
        ///     Create HTTP response from provided text with Content-Type: 'text/plain; charset=UTF-8'
        /// </summary>
        /// <param name="statusCode">response status</param>
        /// <param name="reason">optional reason phrase</param>
        /// <param name="body">optional message body</param>
        /// <returns>
        ///     HTTP response message with provided body and headers
        /// </returns>
        public static ResponseMessage CreatePlainTextResponse(Int32 statusCode, String reason = null, String body = null) {
            var message = new ResponseMessage(String.Format("HTTP/1.1 {0} {1}", statusCode, reason ?? String.Empty)) {
                Body = body != null ? new MemoryStream(Encoding.UTF8.GetBytes(body)) : Stream.Null
            };

            if (body != null) {
                message.EntityHeaders.ContentType = ContentTypeUtf8PlainText;
                message.EntityHeaders.ContentLength = Encoding.UTF8.GetByteCount(body);
            }

            return message;
        }

    }

}