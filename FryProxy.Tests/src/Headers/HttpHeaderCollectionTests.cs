using System;
using System.Collections.Generic;
using FryProxy.Headers;
using NUnit.Framework;

namespace FryProxy.Tests.Headers
{
    public class HttpHeaderCollectionTests : AssertionHelper
    {
        private static readonly IEnumerable<IList<String>> HeadersDataSet = new List<IList<string>>
        {
            new List<String>
            {
                "Cache-Control:private",
                "Connection:close",
                "Content-Encoding:gzip",
                "Content-Type:text/javascript; charset=utf-8",
                "Date:Sat, 21 Mar 2015 19:51:17 GMT",
                "ETag:",
                "Server:Microsoft-IIS/7.5",
                "Vary:Accept-Encoding"
            }
        };

        [TestCaseSource("HeadersDataSet")]
        public void ShouldReturnUnmodifiedHeaders(IList<String> headers)
        {
            var headerCollection = new HttpHeadersCollection(headers);

            Expect(headerCollection.Lines, Is.EqualTo(headers));
        }
    }
}