using System;
using System.Collections.Generic;
using System.Security.Cryptography.X509Certificates;
using System.Threading;
using log4net.Config;
using NUnit.Framework;
using OpenQA.Selenium;
using OpenQA.Selenium.Chrome;
using OpenQA.Selenium.Firefox;
using OpenQA.Selenium.Remote;

namespace FryProxy.Tests.Integration {

    public abstract class IntegrationTestFixture {

        private const String CertificateName = "fry.pfx";

        private const String CertificatePass = "fry";

        static IntegrationTestFixture() {
            BasicConfigurator.Configure();
        }

        protected IWebDriver WebDriver { get; private set; }

        protected HttpProxyServer HttpProxyServer { get; private set; }

        protected HttpProxyServer SslProxyServer { get; private set; }

        [TestFixtureSetUp]
        public void SetUpProxy() {
            var socketTimeout = TimeSpan.FromSeconds(5);

            HttpProxyServer = new HttpProxyServer("localhost", new HttpProxy() {
                ClientWriteTimeout = socketTimeout,
                ServerWriteTimeout = socketTimeout,
                ClientReadTimeout = socketTimeout,
                ServerReadTimeout = socketTimeout
            });

            SslProxyServer = new HttpProxyServer("localhost", new SslProxy(new X509Certificate2(CertificateName, CertificatePass)) {
                ClientWriteTimeout = socketTimeout,
                ServerWriteTimeout = socketTimeout,
                ClientReadTimeout = socketTimeout,
                ServerReadTimeout = socketTimeout
            });

            WaitHandle.WaitAll(
                new[] {
                    HttpProxyServer.Start(),
                    SslProxyServer.Start()
                });
        }

        protected abstract IWebDriver CreateDriver(Proxy proxy);

        protected static IWebDriver CreateFirefoxDriver(Proxy proxy) {
            return new FirefoxDriver(
                new DesiredCapabilities(
                    new Dictionary<String, Object> {
                        {CapabilityType.Proxy, proxy}
                    }));
        }

        protected static IWebDriver CreateChromeDriver(Proxy proxy) {
            return new ChromeDriver(
                new ChromeOptions {
                    Proxy = proxy
                });
        }

        [SetUp]
        public void SetUpBrowser() {
            WebDriver = CreateDriver(
                new Proxy {
                    HttpProxy = String.Format("{0}:{1}", HttpProxyServer.ProxyEndPoint.Address, HttpProxyServer.ProxyEndPoint.Port),
                    SslProxy = String.Format("{0}:{1}", SslProxyServer.ProxyEndPoint.Address, SslProxyServer.ProxyEndPoint.Port),
                    Kind = ProxyKind.Manual
                });
        }

        [TearDown]
        public void CloseBrowser() {
            WebDriver.Quit();
        }

        [TestFixtureTearDown]
        public void ShutdownBrowserAndProxy() {
            HttpProxyServer.Stop();
            SslProxyServer.Stop();
        }

    }

}