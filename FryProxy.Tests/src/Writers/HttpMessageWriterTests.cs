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
                yield return new MessageWriterTestCaseBuilder(new HttpResponseHeader(200, "OK", "1.1"), "ABCD")
                    .SetContentEncodingHeader()
                    .SetContentLengthHeader()
                    .SetBodyLengthArgument()
                    .TestCaseData;

                yield return new MessageWriterTestCaseBuilder(new HttpResponseHeader(200, "OK", "1.1"), "ABCD")
                    .SetContentEncodingHeader()
                    .SetContentLengthHeader()
                    .TestCaseData;

                yield return new MessageWriterTestCaseBuilder(new HttpResponseHeader(200, "OK", "1.1"), "ABCD")
                    .SetContentEncodingHeader()
                    .SetContentLengthHeader()
                    .SetBodyLengthArgument(25L)
                    .TestCaseData;

                yield return new MessageWriterTestCaseBuilder(new HttpResponseHeader(200, "OK", "1.1"), "ABCD")
                    .SetContentEncodingHeader()
                    .SetContentLengthHeader()
                    .SetBodyLengthArgument(0)
                    .TestCaseData;

                yield return new MessageWriterTestCaseBuilder(new HttpResponseHeader(200, "OK", "1.1"), "ABCD")
                    .SetContentEncodingHeader()
                    .SetContentLengthHeader(0)
                    .SetBodyLengthArgument(4)
                    .OverrideExpectedMessageBody(String.Empty)
                    .TestCaseData;

                yield return new MessageWriterTestCaseBuilder(
                    new HttpResponseHeader(200, "OK", "1.1"),
                    new StringBuilder()
                        .AppendLine("0")
                        .AppendLine()
                        .ToString())
                    .SetContentEncodingHeader()
                    .SetChunkedTransferEncoding()
                    .TestCaseData;

                yield return new MessageWriterTestCaseBuilder(
                    new HttpResponseHeader(200, "OK", "1.1"),
                    new StringBuilder()
                        .AppendLine("4")
                        .AppendLine("ABCD")
                        .AppendLine("1A")
                        .AppendLine("and this is the second one")
                        .AppendLine("0")
                        .AppendLine()
                        .ToString())
                    .SetContentEncodingHeader()
                    .SetChunkedTransferEncoding()
                    .TestCaseData;

                yield return new MessageWriterTestCaseBuilder(
                    new HttpResponseHeader(200, "OK", "1.1"), 
                    new StringBuilder()
                        .AppendLine("270F")
                        .AppendLine("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")
                        .AppendLine("1A")
                        .AppendLine("and this is the second one")
                        .AppendLine("0")
                        .AppendLine()
                        .ToString())
                    .SetContentEncodingHeader()
                    .SetChunkedTransferEncoding()
                    .TestCaseData;

                yield return new MessageWriterTestCaseBuilder(new HttpResponseHeader(200, "OK", "1.1"), String.Empty)
                    .SetContentEncodingHeader()
                    .TestCaseData;
            }
        }

        [TestCaseSource("WriteMessageTestCases")]
        public String ShouldWriteHttpMessage(HttpMessageHeader header, Stream body, long? bodyLength)
        {
            var outputStream = new MemoryStream();

            var httpWriter = new HttpMessageWriter(outputStream);

            httpWriter.Write(header, body, bodyLength);

            return Encoding.ASCII.GetString(outputStream.ToArray());
        }
    }
}