using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Sockets;
using FryProxy.Utils;
using NUnit.Framework;

namespace FryProxy.Tests.Utils
{
    public class SocketUtilsTests : AssertionHelper
    {
        private static IEnumerable<ITestCaseData> IsSocketExceptionTestCases
        {
            get
            {
                yield return new TestCaseData(
                    new IOException("Wrong Something", new SocketException((Int32) SocketError.ConnectionAborted)),
                    new[] {SocketError.ConnectionAborted, SocketError.AlreadyInProgress}
                    ).Returns(true);

                yield return new TestCaseData(
                    new SocketException((Int32) SocketError.ConnectionAborted),
                    new[] {SocketError.ConnectionAborted, SocketError.AlreadyInProgress}
                    ).Returns(true);

                yield return new TestCaseData(
                    new Exception("Something Wrong"),
                    new[] {SocketError.ConnectionAborted}
                    ).Returns(false);

                yield return new TestCaseData(
                    new IOException("Everything is Wrong", new SocketException((Int32) SocketError.ConnectionAborted)),
                    new[] {SocketError.AlreadyInProgress, SocketError.ConnectionRefused}
                    ).Returns(false);
            }
        }

        [TestCaseSource("IsSocketExceptionTestCases")]
        public Boolean ShouldReturnWhetherExceptionIsSocketError(Exception exception, SocketError[] errors)
        {
            return SocketUtils.IsSocketException(exception, errors);
        }
    }
}