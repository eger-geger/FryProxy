using System;
using System.Collections.Generic;

using OpenQA.Selenium;
using OpenQA.Selenium.Firefox;
using OpenQA.Selenium.Remote;

namespace FryProxy.Tests {

    public class IntegrationTestFixture {

        protected IWebDriver WebDriver { get; private set; }

        public void SetUpBrowserProxy() {
            WebDriver = new FirefoxDriver(
                new DesiredCapabilities(
                    new Dictionary<String, Object> {
                        {
                            CapabilityType.Proxy, new Proxy {
                                HttpProxy = "",
                                SslProxy = "",
                                Kind = ProxyKind.Manual
                            }
                        }
                    }));
        }

        public void ShutdownBrowserAndProxy() {}

    }

}