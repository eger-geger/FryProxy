using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using FryProxy.Readers;
using NUnit.Framework;

namespace FryProxy.Tests.Readers
{
    public class HttpHeaderReaderTests : AssertionHelper
    {
        [Test]
        public void ShouldReadHttpHeaders()
        {
            var reader = new HttpHeaderReader(new StringReader(
                new StringBuilder(String.Empty)
                    .AppendLine("Cache-Control:private")
                    .AppendLine("Content-Encoding:gzip")
                    .AppendLine("Content-Length:27046")
                    .AppendLine()
                    .ToString()
                ));

            Expect(reader.ReadHeaders(), Is.EqualTo(new List<String>
            {
                "Cache-Control:private",
                "Content-Encoding:gzip",
                "Content-Length:27046"
            }));
        }

        [Test]
        public void ShouldReadFirstNotEmptyLine()
        {
            var reader = new HttpHeaderReader(new StringReader(
                new StringBuilder(String.Empty)
                    .AppendLine()
                    .AppendLine()
                    .AppendLine()
                    .AppendLine("HTTP/1.1 200 OK")
                    .ToString()
                ));

            Expect(reader.ReadFirstLine(), Is.EqualTo("HTTP/1.1 200 OK"));
        }

        [Test]
        public void ShouldReadHttpMessageHeader()
        {
            var reader = new HttpHeaderReader(new StringReader(
                new StringBuilder("HTTP/1.1 200 OK")
                    .AppendLine()
                    .AppendLine("Cache-Control:private")
                    .AppendLine("Content-Encoding:gzip")
                    .AppendLine("Content-Length:27046")
                    .AppendLine()
                    .ToString()
                ));

            var header = reader.ReadHttpMessageHeader();

            Expect(header.StartLine, Is.EqualTo("HTTP/1.1 200 OK"));
            Expect(header.GeneralHeaders.CacheControl, Is.EqualTo("private"));
            Expect(header.EntityHeaders.ContentEncoding, Is.EqualTo("gzip"));
            Expect(header.EntityHeaders.ContentLength, Is.EqualTo(27046));
        }

    }
}