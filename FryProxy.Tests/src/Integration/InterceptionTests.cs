using System.IO;
using System.Text;
using FryProxy.Headers;
using FryProxy.Utility;
using NUnit.Framework;
using OpenQA.Selenium;

namespace FryProxy.Tests.Integration {

    public class InterceptionTests : IntegrationTestFixture {

        protected override IWebDriver CreateDriver(Proxy proxy) {
            return CreateFirefoxDriver(proxy);
        }

        [TearDown]
        public void DetachHandlers() {
            HttpProxyServer.Proxy.OnProcessingComplete = null;
            HttpProxyServer.Proxy.OnRequestReceived = null;
            HttpProxyServer.Proxy.OnResponseReceived = null;
            HttpProxyServer.Proxy.OnResponseSent = null;
            HttpProxyServer.Proxy.OnServerConnected = null;
        }

        [Test]
        public void ShouldRewriteRequest() {
            HttpProxyServer.Proxy.OnRequestReceived = context => {
                var header = context.RequestHeaders;

                if (!header.Host.Contains("wikipedia.org")) {
                    return;
                }

                header.RequestURI = "/";
                header.Host = "example.com";
            };

            WebDriver.Navigate().GoToUrl("http://www.wikipedia.org");

            Assert.That(WebDriver.Title, Contains.Substring("Example Domain"));
        }

        [Test]
        public void ShouldReplaceResponse() {
            var responseBody = new StringBuilder()
                .AppendLine("<html>")
                .AppendLine("<head><title>Fry Rocks!</title></head>")
                .AppendLine("<body><h1>Fry Proxy</h1></body>")
                .AppendLine("</html>")
                .ToString();

            HttpProxyServer.Proxy.OnRequestReceived = context => {
                if (!context.RequestHeaders.Host.Contains("wikipedia.org")) {
                    return;
                }

                context.StopProcessing();

                var responseHeader = new HttpResponseHeaders(HttpResponseExtensions.CreateResponseLine(200, "OK"));
                responseHeader.EntityHeaders.ContentType = "text/html";
                responseHeader.EntityHeaders.ContentEncoding = "us-ascii";
                responseHeader.EntityHeaders.ContentLength = Encoding.ASCII.GetByteCount(responseBody);

                context.ClientStream.SendHttpResponse(responseHeader, new MemoryStream(Encoding.ASCII.GetBytes(responseBody), false));
            };

            WebDriver.Navigate().GoToUrl("http://www.wikipedia.org");

            Assert.That(WebDriver.Title, Contains.Substring("Fry"));
        }

    }

}