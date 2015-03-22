using System.IO;
using System.Text;
using FryProxy.Headers;
using FryProxy.Writers;
using NUnit.Framework;
using OpenQA.Selenium;

namespace FryProxy.Tests.Integration
{
    public class InterceptionTests : IntegrationTestFixture
    {
        protected override IWebDriver CreateDriver(Proxy proxy)
        {
            return CreateFirefoxDriver(proxy);
        }

        [TearDown]
        public void DetachHandlers()
        {
            HttpProxyServer.Proxy.OnProcessingComplete = null;
            HttpProxyServer.Proxy.OnRequestReceived = null;
            HttpProxyServer.Proxy.OnResponseReceived = null;
            HttpProxyServer.Proxy.OnResponseSent = null;
            HttpProxyServer.Proxy.OnServerConnected = null;
        }

        [Test]
        public void ShouldRewriteRequest()
        {
            HttpProxyServer.Proxy.OnRequestReceived = context =>
            {
                HttpRequestHeader header = context.RequestHeader;

                if (!header.Host.Contains("wikipedia.org"))
                {
                    return;
                }

                header.RequestURI = "/";
                header.Host = "example.com";
            };

            WebDriver.Navigate().GoToUrl("http://www.wikipedia.org");

            Assert.That(WebDriver.Title, Contains.Substring("Example Domain"));
        }

        [Test]
        public void ShouldReplaceResponse()
        {
            string responseBody = new StringBuilder()
                .AppendLine("<html>")
                .AppendLine("<head><title>Fry Rocks!</title></head>")
                .AppendLine("<body><h1>Fry Proxy</h1></body>")
                .AppendLine("</html>")
                .ToString();

            HttpProxyServer.Proxy.OnRequestReceived = context =>
            {
                if (!context.RequestHeader.Host.Contains("wikipedia.org"))
                {
                    return;
                }

                context.StopProcessing();

                var responseHeader = new HttpResponseHeader(200, "OK", "1.1");

                responseHeader.EntityHeaders.ContentType = "text/html";
                responseHeader.EntityHeaders.ContentEncoding = "us-ascii";
                responseHeader.EntityHeaders.ContentLength = Encoding.ASCII.GetByteCount(responseBody);

                new HttpResponseWriter(context.ClientStream)
                    .Write(responseHeader, new MemoryStream(Encoding.ASCII.GetBytes(responseBody), false));
            };

            WebDriver.Navigate().GoToUrl("http://www.wikipedia.org");

            Assert.That(WebDriver.Title, Contains.Substring("Fry"));
        }
    }
}