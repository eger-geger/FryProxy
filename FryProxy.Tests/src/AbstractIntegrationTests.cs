using System;

using NUnit.Framework;

namespace FryProxy.Tests {

    public abstract class AbstractIntegrationTests : IntegrationTestFixture {

        [TestCase("http://example.com/", "Example Domain")]
        [TestCase("https://www.wikipedia.org", "Wikipedia")]
        public void ShouldLoadPage(String url, String title) {
            WebDriver.Navigate().GoToUrl(url);

            Assert.That(WebDriver.Title, Is.EqualTo(title));
        }
    }

}