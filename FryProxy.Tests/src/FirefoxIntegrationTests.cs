using NUnit.Framework;

using OpenQA.Selenium;

namespace FryProxy.Tests {

    public class FirefoxIntegrationTests : AbstractIntegrationTests {

        protected override IWebDriver CreateDriver(Proxy proxy) {
            return CreateFirefoxDriver(proxy);
        }

        [Test]
        public void ShouldSentRequest() {
            WebDriver.Navigate().GoToUrl("http://www.stg.justanswer.local/processes/home-page-info.aspx?fid=11&expertSelect=0&expertRealname=1&expertRealnameSingular=1&sipLandingURL=&categoryID=null&hptchid=null&notThisLink=1");
        }

    }

}