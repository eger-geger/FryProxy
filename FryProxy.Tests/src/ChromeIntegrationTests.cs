using OpenQA.Selenium;

namespace FryProxy.Tests {

    public class ChromeIntegratonTests : AbstractIntegrationTests {

        protected override IWebDriver CreateDriver(Proxy proxy) {
            return CreateChromeDriver(proxy);
        }

    }

}