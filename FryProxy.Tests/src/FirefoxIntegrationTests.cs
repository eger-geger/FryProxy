using OpenQA.Selenium;

namespace FryProxy.Tests {

    public class FirefoxIntegrationTests : AbstractIntegrationTests {

        protected override IWebDriver CreateDriver(Proxy proxy) {
            return CreateFirefoxDriver(proxy);
        }

    }

}