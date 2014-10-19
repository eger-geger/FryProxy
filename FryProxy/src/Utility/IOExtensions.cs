using System;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.Globalization;
using System.IO;
using System.Text;

using FryProxy.HttpMessage;

namespace FryProxy.Utility {

    public static class IOExtensions {

        public static void Write(this Stream stream, BaseHttpMessage message) {
            Contract.Requires<ArgumentNullException>(stream != null, "stream");
            Contract.Requires<ArgumentNullException>(message != null, "message");

            var writer = new StreamWriter(stream, Encoding.ASCII);

            writer.WriteLine(message.StartLine);

            foreach (var headerLine in message.Headers.Lines) {
                writer.WriteLine(headerLine);
            }

            writer.WriteLine();
            writer.Flush();

            var contentLength = message.EntityHeaders.ContentLength;

            if (message.Chunked) {
                CopyChunkedTo(message.Body, stream);
            } else if (contentLength.HasValue) {
                CopyTo(message.Body, stream, contentLength.Value, 4096);
            }

            writer.WriteLine();
            writer.Flush();
        }

        public static void CopyChunkedTo(this Stream source, Stream destination) {
            Contract.Requires<ArgumentNullException>(source != null, "source");
            Contract.Requires<ArgumentNullException>(destination != null, "destination");

            var reader = new PlainStreamReader(source);

            var writer = new StreamWriter(destination, Encoding.ASCII) {
                AutoFlush = true
            };

            while (true) {
                var chunkSize = Int32.Parse(ReadNextNonEmptyLine(reader), NumberStyles.HexNumber);

                writer.WriteLine(chunkSize.ToString("X"));

                if (chunkSize == 0) {
                    break;
                }

                CopyTo(source, destination, chunkSize, 4096);

                writer.WriteLine();
            }

            writer.WriteLine();
            writer.WriteLine();
        }

        public static void CopyTo(this Stream source, Stream destination, Int32 amount, Int32 bufferSize) {
            Contract.Requires<ArgumentNullException>(source != null, "source");
            Contract.Requires<ArgumentNullException>(destination != null, "destination");
            Contract.Requires<ArgumentOutOfRangeException>(bufferSize > 0, "bufferSize");

            var buffer = new byte[bufferSize];
            var bytesRead = 0;

            while (bytesRead < amount) {
                var byteCount = Math.Min(amount - bytesRead, bufferSize);

                byteCount = source.Read(buffer, 0, byteCount);
                destination.Write(buffer, 0, byteCount);
                
                bytesRead += byteCount;
            }
        }

        public static ResponseMessage ReadResponseMessage(this Stream stream) {
            Contract.Requires<ArgumentNullException>(stream != null, "stream");

            var reader = new PlainStreamReader(stream);

            return new ResponseMessage(ReadNextNonEmptyLine(reader), new HttpHeaders.HttpHeaders(ReadMessageHeaders(reader))) {
                Body = stream
            };
        }

        public static RequestMessage ReadRequestMessage(this Stream stream) {
            Contract.Requires<ArgumentNullException>(stream != null, "stream");

            var reader = new PlainStreamReader(stream);

            return new RequestMessage(ReadNextNonEmptyLine(reader), new HttpHeaders.HttpHeaders(ReadMessageHeaders(reader))) {
                Body = stream
            };
        }

        public static String ReadNextNonEmptyLine(this TextReader reader) {
            Contract.Requires<ArgumentNullException>(reader != null, "reader");

            var firstLine = String.Empty;

            while (String.IsNullOrWhiteSpace(firstLine)) {
                firstLine = reader.ReadLine();
            }

            return firstLine;
        }

        public static IEnumerable<String> ReadMessageHeaders(this TextReader reader) {
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