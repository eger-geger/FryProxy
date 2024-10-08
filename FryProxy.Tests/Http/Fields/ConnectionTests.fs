module FryProxy.Tests.Http.Fields.ConnectionTests

open FryProxy.Http.Protocol
open FryProxy.Http.Fields
open NUnit.Framework

let connectionKeepAliveTests =
    [ TestCaseData(Http10, None).Returns(false)
      TestCaseData(Http11, None).Returns(true)

      TestCaseData(Http10, Some Connection.KeepAlive).Returns(true)
      TestCaseData(Http11, Some Connection.KeepAlive).Returns(true)

      TestCaseData(Http11, Some Connection.Close).Returns(false)
      TestCaseData(Http10, Some Connection.Close).Returns(false) ]

[<TestCaseSource(nameof connectionKeepAliveTests)>]
let testConnectionIsReusable ver connField = Connection.isPersistent ver connField
