using System;
using System.IO;
using System.Net;

using FryProxy.Headers;

using NUnit.Framework;

using OpenQA.Selenium;

namespace FryProxy.Tests {

    public class ProcessingPipelineTests : IntegrationTestFixture {

        private Stream ServerStream { get; set; }

        private Stream ClientStream { get; set; }

        private HttpRequestHeaders RequestHeaders { get; set; }

        private HttpResponseHeaders ResponseHeaders { get; set; }

        private DnsEndPoint ServerEndPoint { get; set; }

        private ProcessingStage Stage { get; set; }

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

            Stage = 0;
            ClientStream = null;
            ServerStream = null;
            RequestHeaders = null;
            ResponseHeaders = null;
            ServerEndPoint = null;
        }

        private void SaveContext(ProcessingContext context) {
            Console.WriteLine("Saving COntext");
            Stage = context.Stage;
            ClientStream = context.ClientStream;
            ServerStream = context.ServerStream;
            RequestHeaders = context.RequestHeaders;
            ResponseHeaders = context.ResponseHeaders;
            ServerEndPoint = context.ServerEndPoint;
        }

        private void OpenPage() {
            WebDriver.Navigate().GoToUrl("http://example.com/");
        }

        [Test]
        public void ShouldAttachOnRequestReceived() {
            HttpProxyServer.Proxy.OnRequestReceived = SaveContext;

            OpenPage();

            Assert.NotNull(ClientStream);
            Assert.NotNull(RequestHeaders);
            Assert.AreEqual(ProcessingStage.ReceiveRequest, Stage);
        }

        [Test]
        public void ShouldAttachOnServerConnected() {
            HttpProxyServer.Proxy.OnServerConnected = SaveContext;

            OpenPage();

            Assert.AreEqual(ProcessingStage.ConnectToServer, Stage);

            Assert.NotNull(ClientStream);
            Assert.NotNull(RequestHeaders);
            Assert.NotNull(ServerEndPoint);
            Assert.NotNull(ServerStream);
        }

        [Test]
        public void ShouldAttachOnResponseReceived() {
            HttpProxyServer.Proxy.OnResponseReceived = SaveContext;

            OpenPage();

            Assert.AreEqual(ProcessingStage.ReceiveResponse, Stage);

            Assert.NotNull(ClientStream);
            Assert.NotNull(RequestHeaders);
            Assert.NotNull(ServerEndPoint);
            Assert.NotNull(ServerStream);
            Assert.NotNull(ResponseHeaders);
        }

        [Test]
        public void ShouldAttachOnResponseSend() {
            HttpProxyServer.Proxy.OnResponseSent = SaveContext;

            OpenPage();

            Assert.AreEqual(ProcessingStage.SendResponse, Stage);

            Assert.NotNull(ClientStream);
            Assert.NotNull(RequestHeaders);
            Assert.NotNull(ServerEndPoint);
            Assert.NotNull(ServerStream);
            Assert.NotNull(ResponseHeaders);
        }

        [Test]
        public void ShouldAttachOnProcessingComplete() {
            HttpProxyServer.Proxy.OnProcessingComplete = SaveContext;

            OpenPage();

            Assert.AreEqual(ProcessingStage.Completed, Stage);

            Assert.NotNull(ClientStream);
            Assert.NotNull(RequestHeaders);
            Assert.NotNull(ServerEndPoint);
            Assert.NotNull(ServerStream);
            Assert.NotNull(ResponseHeaders);
        }

    }

}