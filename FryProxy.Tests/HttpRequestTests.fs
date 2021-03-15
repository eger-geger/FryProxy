namespace FryProxy.Tests

open System
open System.IO
open System.Text
open FryProxy.Http
open FryProxy.Http.Request
open NUnit.Framework

type HttpRequestTests() =

    static member private httpRequestLineTestCases =
        let makeTestCase (input: string) method uri (version: string) =
            let methodType = HttpMethodType.Parse method
            let uri = Uri(uri, UriKind.RelativeOrAbsolute)
            let version = (Version.Parse version)
            let startLine = RequestLine.create methodType uri version
            TestCaseData(input).Returns(Some startLine)

        seq {
            yield TestCaseData("").Returns(None)
            yield TestCaseData("GET  ").Returns(None)
            yield TestCaseData("GET / ").Returns(None)
            yield TestCaseData("GET / HTTP").Returns(None)
            yield TestCaseData("GET \ HTTP").Returns(None)
            yield TestCaseData("GET / HTTP/a.a").Returns(None)
            yield TestCaseData("GFD / HTTP/1.0").Returns(None)

            yield makeTestCase "GET / HTTP/1.0" "GET" "/" "1.0"
            yield makeTestCase "GET * HTTP/1.1" "GET" "*" "1.1"
            yield makeTestCase "POST /checkout/ HTTP/1.1" "POST" "/checkout/" "1.1"
            yield makeTestCase "OPTIONS google.com:80 HTTP/1.1" "OPTIONS" "google.com:80" "1.1"
            yield makeTestCase "DELETE http://google.com HTTP/1.1" "DELETE" "http://google.com" "1.1"

            yield
                makeTestCase
                    "HEAD google.com/search?q=hello+world HTTP/1.1"
                    "HEAD"
                    "google.com/search?q=hello+world"
                    "1.1"

        }

    [<TestCaseSource("httpRequestLineTestCases")>]
    member this.testParseHttpRequestLine(line: string) = RequestLine.tryParse line

    static member private messageHeaderTestCases =
        seq {
            yield
                TestCaseData([ "Accept: application/json" ])
                    .Returns(None)

            yield
                TestCaseData([ "POST google.com HTTP/1.1" ])
                    .Returns(Some
                                 ({ method = HttpMethodType.POST
                                    uri = Uri("google.com", UriKind.Relative)
                                    version = Version(1, 1) },
                                  List.empty<HttpHeader>))

            yield
                TestCaseData([ "GET / HTTP/1.1"; "Accept: application/json" ])
                    .Returns(Some
                                 ({ method = HttpMethodType.GET
                                    uri = Uri("/", UriKind.Relative)
                                    version = Version(1, 1) },
                                  [ { name = "Accept"; values = [ "application/json" ] } ]))

            yield
                TestCaseData([ "GET https://google.com/ HTTP/1.1"
                               "Accept: text/html,application/xml"
                               "Accept-Encoding: gzip, deflate, br"
                               "User-Agent: Chrome/88.0.4324.146" ])
                    .Returns(Some
                                 ({ method = HttpMethodType.GET
                                    uri = Uri("https://google.com", UriKind.RelativeOrAbsolute)
                                    version = Version(1, 1) },
                                  [ { name = "Accept"; values = [ "text/html"; "application/xml" ] }
                                    { name = "Accept-Encoding"; values = [ "gzip"; "deflate"; "br" ] }
                                    { name = "User-Agent"; values = [ "Chrome/88.0.4324.146" ] } ]))
        }

    [<TestCaseSource("messageHeaderTestCases")>]
    member this.testParseHttpRequestHeaders(lines) = tryParseHeaders lines

    [<TestCaseSource("messageHeaderTestCases")>]
    member this.testReadHttpRequestHeaders(lines: seq<string>) =
        let appendLine (sb: StringBuilder) = sb.AppendLine
        let builder = lines |> Seq.fold appendLine (StringBuilder())
        use stream = new MemoryStream(Encoding.ASCII.GetBytes(builder.ToString()))

        readHeaders stream |> tryParseHeaders
