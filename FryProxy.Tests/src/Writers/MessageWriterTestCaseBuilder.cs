using System;
using System.IO;
using System.Text;
using FryProxy.Headers;
using NUnit.Framework;

namespace FryProxy.Tests.Writers
{
    public class MessageWriterTestCaseBuilder
    {
        private readonly HttpMessageHeader _httpMessageHeader;

        private readonly String _messageBody;

        private long? _bodyLengthArgument;

        private String _expectedMessageBody;

        public MessageWriterTestCaseBuilder(HttpMessageHeader header, String body)
        {
            _messageBody = body;
            _httpMessageHeader = header;
        }

        private String ExpectedResult
        {
            get
            {
                StringBuilder expectedResult = new StringBuilder().AppendLine(_httpMessageHeader.StartLine);

                foreach (string headerLine in _httpMessageHeader.Headers.Lines)
                {
                    expectedResult.AppendLine(headerLine);
                }

                expectedResult.AppendLine();

                if (_httpMessageHeader.GeneralHeaders.TransferEncoding == "chunked")
                {
                    expectedResult.Append(_expectedMessageBody ?? _messageBody);
                }
                else
                {
                    if (!String.IsNullOrEmpty(_expectedMessageBody))
                    {
                        expectedResult.AppendLine(_expectedMessageBody);
                    }
                    else if (!String.IsNullOrEmpty(_messageBody))
                    {
                        expectedResult.AppendLine(_messageBody);
                    }
                }

                return expectedResult.ToString();
            }
        }

        public ITestCaseData TestCaseData
        {
            get
            {
                return new TestCaseData(
                    _httpMessageHeader,
                    new MemoryStream(Encoding.ASCII.GetBytes(_messageBody)),
                    _bodyLengthArgument
                    ).Returns(ExpectedResult);
            }
        }

        public MessageWriterTestCaseBuilder SetContentEncodingHeader()
        {
            _httpMessageHeader.EntityHeaders.ContentEncoding = "us-ascii";

            return this;
        }

        public MessageWriterTestCaseBuilder SetContentLengthHeader(int? contentLengthHeader = null)
        {
            _httpMessageHeader.EntityHeaders.ContentLength =
                contentLengthHeader.GetValueOrDefault(_messageBody.Length);

            return this;
        }

        public MessageWriterTestCaseBuilder SetBodyLengthArgument(long? bodyLengthArgument = null)
        {
            _bodyLengthArgument = bodyLengthArgument.GetValueOrDefault(_messageBody.Length);

            return this;
        }

        public MessageWriterTestCaseBuilder OverrideExpectedMessageBody(String messageBody)
        {
            _expectedMessageBody = messageBody;
                
            return this;
        }

        public MessageWriterTestCaseBuilder SetChunkedTransferEncoding()
        {
            _httpMessageHeader.GeneralHeaders.TransferEncoding = "chunked";

            return this;
        }
    }
}