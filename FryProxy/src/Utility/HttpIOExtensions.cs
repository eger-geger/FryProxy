using System;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.Globalization;
using System.IO;
using System.Text;

using FryProxy.Headers;

namespace FryProxy.Utility {

    public static class HttpIOExtensions {

        /// <summary>
        ///     Write HTTP message to stream
        /// </summary>
        /// <param name="outputStream">stream message will be written to</param>
        /// <param name="headers">HTTP message headers</param>
        /// <param name="contentStream">stream message content will be taken from</param>
        /// <param name="bufferSize">size of buffer which will be used for copying message body</param>
        public static void WriteHttpMessage(this Stream outputStream, HttpMessageHeaders headers, Stream contentStream, Int32 bufferSize) {
            Contract.Requires<ArgumentNullException>(outputStream != null, "stream");
            Contract.Requires<ArgumentNullException>(headers != null, "headers");
            Contract.Requires<ArgumentOutOfRangeException>(bufferSize > 0, "bufferSize");

            var writer = new StreamWriter(outputStream, Encoding.ASCII) {
                AutoFlush = true
            };

            writer.WriteLine(headers.StartLine);

            foreach (var headerLine in headers.HeadersCollection.Lines) {
                writer.WriteLine(headerLine);
            }

            writer.WriteLine();

            if (contentStream != null) {
                contentStream.CopyMessageBody(outputStream, headers, bufferSize);
            }

            writer.WriteLine();
        }

        /// <summary>
        ///     Copy HTTP message body from one stream to another
        /// </summary>
        /// <param name="sourceStream">stream to read content from</param>
        /// <param name="destinationStream">stream to write content to</param>
        /// <param name="headers">message headers which will be used for defining message type and length</param>
        /// <param name="bufferSize">size of buffer which will be used for copying stream content</param>
        public static void CopyMessageBody(this Stream sourceStream, Stream destinationStream, HttpMessageHeaders headers, Int32 bufferSize) {
            Contract.Requires<ArgumentNullException>(headers != null, "headers");
            Contract.Requires<ArgumentOutOfRangeException>(bufferSize > 0, "bufferSize");
            Contract.Requires<ArgumentNullException>(sourceStream != null, "sourceStream");
            Contract.Requires<ArgumentNullException>(destinationStream != null, "destinationStream");

            var contentLength = headers.EntityHeaders.ContentLength;

            if (headers.Chunked) {
                sourceStream.CopyChunkedMessageBody(destinationStream, bufferSize);
            } else if (contentLength.HasValue) {
                sourceStream.CopyPlainMessageBody(destinationStream, contentLength.Value, bufferSize);
            }
        }

        /// <summary>
        ///     Copy all parts of chuncked HTTP message body from one stream to another
        /// </summary>
        /// <param name="sourceStream">stream to read content from</param>
        /// <param name="destinationStream">stream to write content to</param>
        /// <param name="bufferSize">size of buffer which will be used for copying stream content</param>
        public static void CopyChunkedMessageBody(this Stream sourceStream, Stream destinationStream, Int32 bufferSize) {
            Contract.Requires<ArgumentNullException>(sourceStream != null, "sourceStream");
            Contract.Requires<ArgumentNullException>(destinationStream != null, "destinationStream");
            Contract.Requires<ArgumentOutOfRangeException>(bufferSize > 0, "bufferSize");

            var reader = new PlainStreamReader(sourceStream);

            var writer = new StreamWriter(destinationStream, Encoding.ASCII) {
                AutoFlush = true
            };

            while (true) {
                var chunkSize = Int32.Parse(ReadStatusLine(reader), NumberStyles.HexNumber);

                writer.WriteLine(chunkSize.ToString("X"));

                if (chunkSize == 0) {
                    break;
                }

                sourceStream.CopyPlainMessageBody(destinationStream, chunkSize, bufferSize);

                writer.WriteLine();
            }

            writer.WriteLine();
            writer.WriteLine();
        }

        /// <summary>
        ///     Copy plain HTTP message body from one stream to another
        /// </summary>
        /// <param name="sourceStream">stream to read content from</param>
        /// <param name="destinationStream">stream to write content to</param>
        /// <param name="contentLength">how many bytes to copy</param>
        /// <param name="bufferSize">size of buffer which will be used for copying stream content</param>
        public static void CopyPlainMessageBody(this Stream sourceStream, Stream destinationStream, Int32 contentLength, Int32 bufferSize) {
            Contract.Requires<ArgumentNullException>(sourceStream != null, "sourceStream");
            Contract.Requires<ArgumentNullException>(destinationStream != null, "destinationStream");
            Contract.Requires<ArgumentOutOfRangeException>(bufferSize > 0, "bufferSize");

            var buffer = new byte[bufferSize];
            var bytesRead = 0;

            while (bytesRead < contentLength) {
                var byteCount = Math.Min(contentLength - bytesRead, bufferSize);

                byteCount = sourceStream.Read(buffer, 0, byteCount);
                destinationStream.Write(buffer, 0, byteCount);

                bytesRead += byteCount;
            }
        }

        public static HttpResponseHeaders ReadResponseHeaders(this Stream stream) {
            Contract.Requires<ArgumentNullException>(stream != null, "stream");

            var reader = new PlainStreamReader(stream);

            return new HttpResponseHeaders(ReadStatusLine(reader)) {
                HeadersCollection = new HttpHeadersCollection(ReadRawHeaders(reader)),
            };
        }

        public static HttpRequestHeaders ReadRequestHeaders(this Stream stream) {
            Contract.Requires<ArgumentNullException>(stream != null, "stream");

            var reader = new PlainStreamReader(stream);

            return new HttpRequestHeaders(ReadStatusLine(reader)) {
                HeadersCollection = new HttpHeadersCollection(ReadRawHeaders(reader)),
            };
        }

        public static String ReadStatusLine(this TextReader reader) {
            Contract.Requires<ArgumentNullException>(reader != null, "reader");

            var firstLine = String.Empty;

            while (String.IsNullOrWhiteSpace(firstLine)) {
                firstLine = reader.ReadLine();
            }

            return firstLine;
        }

        public static IEnumerable<String> ReadRawHeaders(this TextReader reader) {
            Contract.Requires<ArgumentNullException>(reader != null, "reader");

            var headers = new List<String>();

            while (true) {
                var header = reader.ReadLine();

                if (String.IsNullOrEmpty(header)) {
                    break;
                }

                headers.Add(header);
            }

            return headers;
        }

    }

}