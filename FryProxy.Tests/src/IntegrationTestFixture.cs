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

namespace FryProxy.Tests {

    public class IntegrationTestFixture {

        private const String CertificateName = "fry.pfx";

        private const String CertificatePass = "fry";

        public IntegrationTestFixture() {
            BasicConfigurator.Configure();
        }

        protected IWebDriver WebDriver { get; private set; }

        protected HttpProxyServer HttpProxyServer { get; private set; }

        protected HttpProxyServer SslProxyServer { get; private set; }

        [TestFixtureSetUp]
        public void SetUpBrowserProxy() {
            HttpProxyServer = new HttpProxyServer("localhost", new HttpProxy());
            SslProxyServer = new HttpProxyServer("localhost", new SslProxy(new X509Certificate2(CertificateName, CertificatePass)));

            WaitHandle.WaitAll(
                new[] {
                    HttpProxyServer.Start(),
                    SslProxyServer.Start()
                });

            var proxy = new Proxy {
                HttpProxy = String.Format("{0}:{1}", HttpProxyServer.ProxyEndPoint.Address, HttpProxyServer.ProxyEndPoint.Port),
                SslProxy = String.Format("{0}:{1}", SslProxyServer.ProxyEndPoint.Address, SslProxyServer.ProxyEndPoint.Port),
                Kind = ProxyKind.Manual
            };

            WebDriver = new FirefoxDriver(
                new DesiredCapabilities(
                    new Dictionary<string, object> {
                        {CapabilityType.Proxy, proxy}
                    }));

//            WebDriver = new ChromeDriver(
//                new ChromeOptions {
//                    Proxy = proxy
//                });
        }

        [TestFixtureTearDown]
        public void ShutdownBrowserAndProxy() {
//            WebDriver.Quit();
            HttpProxyServer.Stop();
            SslProxyServer.Stop();
        }

    }

}