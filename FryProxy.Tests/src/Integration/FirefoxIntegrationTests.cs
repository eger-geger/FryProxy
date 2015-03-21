using OpenQA.Selenium;

namespace FryProxy.Tests.Integration {

    public class FirefoxIntegrationTests : AbstractIntegrationTests {

        protected override IWebDriver CreateDriver(Proxy proxy) {
            return CreateFirefoxDriver(proxy);
        }

    }

}