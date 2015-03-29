using System;
using System.Diagnostics.Contracts;
using System.Globalization;
using System.IO;
using System.Text;
using FryProxy.Headers;
using FryProxy.Readers;
using FryProxy.Utils;
using log4net;

namespace FryProxy.Writers
{
    /// <summary>
    ///     Writes HTTP message to underlying stream
    /// </summary>
    public class HttpMessageWriter
    {
        protected static readonly ILog Logger = LogManager.GetLogger(typeof (HttpMessageWriter));

        protected const Int32 BufferSize = 8192;

        protected readonly Stream OutputStream;

        /// <summary>
        ///     Creates new writer instance, writing to provided stream
        /// </summary>
        /// <param name="outputStream">stream which will be written to</param>
        public HttpMessageWriter(Stream outputStream)
        {
            if (outputStream == null)
            {
                throw new ArgumentNullException("outputStream");
            }

            OutputStream = outputStream;
        }

        /// <summary>
        ///     Writes HTTP message to wrapped stream
        /// </summary>
        /// <param name="header">HTTP message header</param>
        /// <param name="body">HTTP message body</param>
        /// <param name="bodyLength">expected length of HTTP message body</param>
        public void Write(HttpMessageHeader header, Stream body = null, Nullable<Int64> bodyLength = null)
        {
            Contract.Requires<ArgumentNullException>(header != null, "header");

            var writer = new StreamWriter(OutputStream, Encoding.ASCII);

            writer.WriteLine(header.StartLine);

            foreach (String headerLine in header.Headers.Lines)
            {
                writer.WriteLine(headerLine);
            }

            writer.WriteLine();
            writer.Flush();

            if (body == null)
            {
                return;
            } 
            
            WriteBody(header, body, bodyLength.GetValueOrDefault(0));
            
            writer.WriteLine();
            writer.Flush();
        }

        /// <summary>
        ///     Writes messag body to <seealso cref="OutputStream"/>
        /// </summary>
        /// <param name="header">HTTP message header</param>
        /// <param name="body">HTTP message body</param>
        /// <param name="bodyLength">expected length of HTTP message body</param>
        protected virtual void WriteBody(HttpMessageHeader header, Stream body, Int64 bodyLength)
        {
            if (header.Chunked)
            {
                CopyChunkedMessageBody(body);
            }
            else if (header.EntityHeaders.ContentLength.HasValue)
            {
                CopyPlainMessageBody(body, header.EntityHeaders.ContentLength.Value);
            }
            else if (bodyLength > 0)
            {
                body.CopyTo(OutputStream);
            }
            else
            {
                if (Logger.IsDebugEnabled)
                {
                    Logger.Debug("Message body is empty");
                }
            }
        }

        private void CopyPlainMessageBody(Stream body, Int64 contentLength)
        {
            var buffer = new Byte[BufferSize];

            Int64 totalBytesRead = 0;

            while (totalBytesRead < contentLength)
            {
                var bytesCopied = body.Read(buffer, 0, (Int32) Math.Min(buffer.Length, contentLength));

                OutputStream.Write(buffer, 0, bytesCopied);

                totalBytesRead += bytesCopied;
            }
        }

        /// <summary>
        ///     Copy chunked message body to <see cref="OutputStream" /> from given stream
        /// </summary>
        /// <param name="body">chunked HTTP message body</param>
        protected virtual void CopyChunkedMessageBody(Stream body)
        {
            var reader = new HttpHeaderReader(new PlainStreamReader(body));

            var writer = new StreamWriter(OutputStream, Encoding.ASCII);

            for (var size = reader.ReadNextChunkSize();size != 0;size = reader.ReadNextChunkSize())
            {
                writer.WriteLine(size.ToString("X"));
                writer.Flush();

                CopyPlainMessageBody(body, size);

                writer.WriteLine();
                writer.Flush();
            }

            writer.WriteLine("0");
            writer.Flush();
        }
    }
}