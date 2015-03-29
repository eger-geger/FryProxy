using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using FryProxy.Headers;
using FryProxy.Writers;
using NUnit.Framework;

namespace FryProxy.Tests.Writers
{
    public class HttpMessageWriterTests
    {
        private static IEnumerable<ITestCaseData> WriteMessageTestCases
        {
            get
            {
                var messageHeader = new HttpResponseHeader(200, "OK", "1.1");
                messageHeader.EntityHeaders.ContentEncoding = "us-ascii";
                messageHeader.EntityHeaders.ContentLength = 4;

                var memoryStream = new MemoryStream(Encoding.ASCII.GetBytes("ABCD"));

                yield return new TestCaseData(messageHeader, memoryStream, memoryStream.Length)
                    .Returns(new StringBuilder()
                        .AppendLine("HTTP/1.1 200 OK")
                        .AppendLine("Content-Encoding:us-ascii")
                        .AppendLine("Content-Length:" + memoryStream.Length)
                        .AppendLine()
                        .AppendLine("ABCD")
                        .ToString()
                    );

                memoryStream = new MemoryStream(Encoding.ASCII.GetBytes("ABCD"));

                yield return new TestCaseData(messageHeader, memoryStream, null)
                    .Returns(new StringBuilder()
                        .AppendLine("HTTP/1.1 200 OK")
                        .AppendLine("Content-Encoding:us-ascii")
                        .AppendLine("Content-Length:" + memoryStream.Length)
                        .AppendLine()
                        .AppendLine("ABCD")
                        .ToString()
                    );

                memoryStream = new MemoryStream(Encoding.ASCII.GetBytes("ABCD"));

                yield return new TestCaseData(messageHeader, memoryStream, 25L)
                    .Returns(new StringBuilder()
                        .AppendLine("HTTP/1.1 200 OK")
                        .AppendLine("Content-Encoding:us-ascii")
                        .AppendLine("Content-Length:" + memoryStream.Length)
                        .AppendLine()
                        .AppendLine("ABCD")
                        .ToString()
                    );

                messageHeader = new HttpResponseHeader(200, "OK", "1.1");
                messageHeader.EntityHeaders.ContentEncoding = "us-ascii";

                memoryStream = new MemoryStream(Encoding.ASCII.GetBytes("ABCD"));

                yield return new TestCaseData(messageHeader, memoryStream, memoryStream.Length)
                    .Returns(new StringBuilder()
                        .AppendLine("HTTP/1.1 200 OK")
                        .AppendLine("Content-Encoding:us-ascii")
                        .AppendLine()
                        .AppendLine("ABCD")
                        .ToString()
                    );

                messageHeader = new HttpResponseHeader(200, "OK", "1.1");
                messageHeader.EntityHeaders.ContentEncoding = "us-ascii";

                memoryStream = new MemoryStream(Encoding.ASCII.GetBytes("ABCD"));

                yield return new TestCaseData(messageHeader, memoryStream, 0L)
                    .Returns(new StringBuilder()
                        .AppendLine("HTTP/1.1 200 OK")
                        .AppendLine("Content-Encoding:us-ascii")
                        .AppendLine()
                        .AppendLine()
                        .ToString()
                    );

                messageHeader = new HttpResponseHeader(200, "OK", "1.1");
                messageHeader.EntityHeaders.ContentEncoding = "us-ascii";
                messageHeader.EntityHeaders.ContentLength = 0;

                memoryStream = new MemoryStream(Encoding.ASCII.GetBytes("ABCD"));

                yield return new TestCaseData(messageHeader, memoryStream, memoryStream.Length)
                    .Returns(new StringBuilder()
                        .AppendLine("HTTP/1.1 200 OK")
                        .AppendLine("Content-Encoding:us-ascii")
                        .AppendLine("Content-Length:0")
                        .AppendLine()
                        .AppendLine()
                        .ToString()
                    );

                messageHeader = new HttpResponseHeader(200, "OK", "1.1");
                messageHeader.GeneralHeaders.TransferEncoding = "chunked";
                messageHeader.EntityHeaders.ContentEncoding = "us-ascii";

                memoryStream = new MemoryStream(
                    Encoding.ASCII.GetBytes(new StringBuilder()
                        .AppendLine("23")
                        .AppendLine("This is the data in the first chunk")
                        .AppendLine("1A")
                        .AppendLine("and this is the second one")
                        .AppendLine("0")
                        .AppendLine()
                        .ToString())
                    );

                yield return new TestCaseData(messageHeader, memoryStream, memoryStream.Length)
                    .Returns(new StringBuilder()
                        .AppendLine("HTTP/1.1 200 OK")
                        .AppendLine("Transfer-Encoding:chunked")
                        .AppendLine("Content-Encoding:us-ascii")
                        .AppendLine()
                        .AppendLine("23")
                        .AppendLine("This is the data in the first chunk")
                        .AppendLine("1A")
                        .AppendLine("and this is the second one")
                        .AppendLine("0")
                        .AppendLine()
                        .ToString()
                    );
            }
        }

        [TestCaseSource("WriteMessageTestCases")]
        public String ShouldWriteHttpMessage(HttpMessageHeader header, Stream body, Nullable<Int64> bodyLength)
        {
            var outputStream = new MemoryStream();

            var httpWriter = new HttpMessageWriter(outputStream);

            httpWriter.Write(header, body, bodyLength);

            return Encoding.ASCII.GetString(outputStream.ToArray());
        }
    }
}