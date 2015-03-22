using System;
using System.IO;

namespace FryProxy.Writers
{
    /// <summary>
    ///     HTTP message writer with optimizations for HTTP request messages
    /// </summary>
    public class HttpRequestWriter : HttpMessageWriter {

        /// <summary>
        ///     
        /// </summary>
        /// <param name="outputStream"></param>
        public HttpRequestWriter(Stream outputStream) : base(outputStream)
        {
        }

        protected override void CopyPlainMessageBody(Stream body, Nullable<Int64> contentLength)
        {
            base.CopyPlainMessageBody(body, contentLength.GetValueOrDefault(0));
        }
    }
}
