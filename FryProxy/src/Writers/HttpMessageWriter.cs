using System;
using System.Diagnostics.Contracts;
using System.Globalization;
using System.IO;
using System.Text;
using FryProxy.Headers;
using FryProxy.Readers;
using FryProxy.Utility;

namespace FryProxy.Writers
{
    /// <summary>
    ///     Writes HTTP message to underlying stream
    /// </summary>
    public class HttpMessageWriter
    {
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
        public void Write(HttpMessageHeader header, Stream body = null)
        {
            Contract.Requires<ArgumentNullException>(header != null, "header");

            var writer = new StreamWriter(OutputStream, Encoding.ASCII);

            writer.WriteLine(header.StartLine);

            foreach (string headerLine in header.Headers.Lines)
            {
                writer.WriteLine(headerLine);
            }

            writer.WriteLine();
            writer.Flush();

            if (body == null)
            {
                return;
            }

            if (header.Chunked)
            {
                CopyChunkedMessageBody(body);
            }
            else
            {
                CopyPlainMessageBody(body, header.EntityHeaders.ContentLength);
            }

            writer.WriteLine();
            writer.Flush();
        }

        /// <summary>
        ///     Copy HTTP message body to <see cref="OutputStream" /> from provided stream
        /// </summary>
        /// <param name="body">source of HTTP message body</param>
        /// <param name="contentLength">'Content-Lenght' header value or null</param>
        protected virtual void CopyPlainMessageBody(Stream body, long? contentLength)
        {
            var buffer = new Byte[BufferSize];

            if (!contentLength.HasValue)
            {
                body.CopyTo(OutputStream, BufferSize);
            }
            else
            {
                var totalBytesRead = 0;

                while (totalBytesRead < contentLength)
                {
                    var bytesRead = body.Read(buffer, 0, Math.Min((Int32) (contentLength.Value - totalBytesRead), BufferSize));

                    OutputStream.Write(buffer, 0, bytesRead);

                    totalBytesRead += bytesRead;
                }
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

            for (var chunkSize = Int32.Parse(reader.ReadFirstLine(), NumberStyles.HexNumber);
                chunkSize != 0;
                chunkSize = Int32.Parse(reader.ReadFirstLine(), NumberStyles.HexNumber))
            {
                writer.WriteLine(chunkSize.ToString("X"));
                writer.Flush();

                CopyPlainMessageBody(body, chunkSize);

                writer.WriteLine();
                writer.Flush();
            }

            writer.WriteLine("0");
            writer.Flush();
        }
    }
}