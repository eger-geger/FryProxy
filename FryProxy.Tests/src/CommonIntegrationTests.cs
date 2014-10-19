using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;

namespace FryProxy.Tests
{
    public class CommonIntegrationTests : IntegrationTestFixture {

        [Test, Ignore]
        public void ShouldReceiveResponse() {
            WebDriver.Navigate().GoToUrl("https://www.google.com");

            Assert.IsNotEmpty(WebDriver.PageSource);
        }

        [Test]
        public void ShouldLoadPageOverHttp() {
            WebDriver.Navigate().GoToUrl("http://ya.ru");

            File.WriteAllText("ya.ru.html", WebDriver.PageSource);

            Assert.IsNotEmpty(WebDriver.PageSource);
        }

    }
}
