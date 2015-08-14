using System.IO;
using System.Net;
using System.Threading;
using FryProxy.Headers;
using NUnit.Framework;
using OpenQA.Selenium;
using HttpRequestHeader = FryProxy.Headers.HttpRequestHeader;
using HttpResponseHeader = FryProxy.Headers.HttpResponseHeader;

namespace FryProxy.Tests.Integration {

    public class ProcessingPipelineTests : IntegrationTestFixture {

        private readonly AutoResetEvent _callbackWaitHandle = new AutoResetEvent(false);

        private Stream ServerStream { get; set; }

        private Stream ClientStream { get; set; }

        private HttpRequestHeader RequestHeader { get; set; }

        private HttpResponseHeader ResponseHeader { get; set; }

        private DnsEndPoint ServerEndPoint { get; set; }

        private ProcessingStage Stage { get; set; }

        protected override IWebDriver CreateDriver(Proxy proxy) {
            return CreateChromeDriver(proxy);
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
            RequestHeader = null;
            ResponseHeader = null;
            ServerEndPoint = null;
        }

        private void SaveContext(ProcessingContext context) {
            Stage = context.Stage;
            ClientStream = context.ClientStream;
            ServerStream = context.ServerStream;
            RequestHeader = context.RequestHeader;
            ResponseHeader = context.ResponseHeader;
            ServerEndPoint = context.ServerEndPoint;

            _callbackWaitHandle.Set();
        }

        private void OpenPage() {
            WebDriver.Navigate().GoToUrl("http://example.com/");
            _callbackWaitHandle.WaitOne();
        }

        [Test]
        public void ShouldAttachOnRequestReceived() {
            HttpProxyServer.Proxy.OnRequestReceived = SaveContext;

            OpenPage();

            Assert.NotNull(ClientStream);
            Assert.NotNull(RequestHeader);
            Assert.AreEqual(ProcessingStage.ReceiveRequest, Stage);
        }

        [Test]
        public void ShouldAttachOnServerConnected() {
            HttpProxyServer.Proxy.OnServerConnected = SaveContext;

            OpenPage();

            Assert.AreEqual(ProcessingStage.ConnectToServer, Stage);

            Assert.NotNull(ClientStream);
            Assert.NotNull(RequestHeader);
            Assert.NotNull(ServerEndPoint);
            Assert.NotNull(ServerStream);
        }

        [Test]
        public void ShouldAttachOnResponseReceived() {
            HttpProxyServer.Proxy.OnResponseReceived = SaveContext;

            OpenPage();

            Assert.AreEqual(ProcessingStage.ReceiveResponse, Stage);

            Assert.NotNull(ClientStream);
            Assert.NotNull(RequestHeader);
            Assert.NotNull(ServerEndPoint);
            Assert.NotNull(ServerStream);
            Assert.NotNull(ResponseHeader);
        }

        [Test]
        public void ShouldAttachOnResponseSend() {
            HttpProxyServer.Proxy.OnResponseSent = SaveContext;

            OpenPage();

            Assert.AreEqual(ProcessingStage.SendResponse, Stage);

            Assert.NotNull(ClientStream);
            Assert.NotNull(RequestHeader);
            Assert.NotNull(ServerEndPoint);
            Assert.NotNull(ServerStream);
            Assert.NotNull(ResponseHeader);
        }

        [Test]
        public void ShouldAttachOnProcessingComplete() {
            HttpProxyServer.Proxy.OnProcessingComplete = SaveContext;

            OpenPage();

            Assert.AreEqual(ProcessingStage.Completed, Stage);

            Assert.NotNull(ClientStream);
            Assert.NotNull(RequestHeader);
            Assert.NotNull(ServerEndPoint);
            Assert.NotNull(ServerStream);
            Assert.NotNull(ResponseHeader);
        }

    }

}