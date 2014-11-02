using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;

namespace FryProxy.Tests
{
    public class CommonIntegrationTests : IntegrationTestFixture {

        [Test]
        public void ShouldLoadPageOverHttps() {
            WebDriver.Navigate().GoToUrl("https://www.google.com");

            File.WriteAllText("google.com.html", WebDriver.PageSource);

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
