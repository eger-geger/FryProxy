namespace FryProxy.Tests

open System
open System.IO
open System.Text
open FryProxy.Http
open FryProxy.Http.HttpMessage
open NUnit.Framework

type HttpMessageTests() =

    static member private startLineTestCases =
        let makeTestCase (input: string) method uri (version: string) =
            let methodType = HttpMethodType.Parse method
            let uri = Uri(uri, UriKind.RelativeOrAbsolute)
            let version = (Version.Parse version)
            let startLine = makeStartLine methodType uri version
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

    [<TestCaseSource("startLineTestCases")>]
    member this.testParseStartLine(line: string) = tryParseStartLine line

    static member private headerValueTestCases =
        seq {
            yield TestCaseData("").Returns(List.empty<string>)

            yield
                TestCaseData("application/json")
                    .Returns([ "application/json" ])

            yield
                TestCaseData("application/json ")
                    .Returns([ "application/json" ])

            yield
                TestCaseData(" application/json")
                    .Returns([ "application/json" ])

            yield
                TestCaseData("application/json , text/xml")
                    .Returns([ "application/json"; "text/xml" ])
        }

    [<TestCaseSource("headerValueTestCases")>]
    member this.testParseHeaderValue(value) = parseHeaderValue value

    static member headerLineTestCases =
        seq {
            yield TestCaseData("").Returns(None)
            yield TestCaseData("Accept").Returns(None)
            yield TestCaseData("Accept:").Returns(None)

            yield
                TestCaseData("Accept: application/json")
                    .Returns(Some(makeHttpHeader "Accept" [ "application/json" ]))

            yield
                TestCaseData("Accept: application/json,application/xml")
                    .Returns(Some(makeHttpHeader "Accept" [ "application/json"; "application/xml" ]))
        }

    [<TestCaseSource("headerLineTestCases")>]
    member this.testParseHeaderLine(line) = tryParseHeaderLine line


    static member private messageHeaderTestCases =
        seq {
            yield
                TestCaseData([ "Accept: application/json" ])
                    .Returns(None)

            yield
                TestCaseData([ "POST google.com HTTP/1.1" ])
                    .Returns({ startLine =
                                   { method = HttpMethodType.POST
                                     uri = Uri("google.com", UriKind.Relative)
                                     version = Version(1, 1) }
                               headers = [] }
                             |> Some)

            yield
                TestCaseData([ "GET / HTTP/1.1"; "Accept: application/json" ])
                    .Returns({ startLine =
                                   { method = HttpMethodType.GET
                                     uri = Uri("/", UriKind.Relative)
                                     version = Version(1, 1) }
                               headers = [ { name = "Accept"; values = [ "application/json" ] } ] }
                             |> Some)

            yield
                TestCaseData([ "GET https://google.com/ HTTP/1.1"
                               "Accept: text/html,application/xml"
                               "Accept-Encoding: gzip, deflate, br"
                               "User-Agent: Chrome/88.0.4324.146" ])
                    .Returns({ startLine =
                                   { method = HttpMethodType.GET
                                     uri = Uri("https://google.com", UriKind.RelativeOrAbsolute)
                                     version = Version(1, 1) }
                               headers =
                                   [ { name = "Accept"; values = [ "text/html"; "application/xml" ] }
                                     { name = "Accept-Encoding"; values = [ "gzip"; "deflate"; "br" ] }
                                     { name = "User-Agent"; values = [ "Chrome/88.0.4324.146" ] } ] }
                             |> Some)
        }

    [<TestCaseSource("messageHeaderTestCases")>]
    member this.testParseMessageHeader(lines) = tryParseMessageHeader lines

    [<TestCaseSource("messageHeaderTestCases")>]
    member this.testReadMessageHeader(lines: seq<string>) =
        let appendLine (sb: StringBuilder) = sb.AppendLine
        let builder = lines |> Seq.fold appendLine (StringBuilder())
        
        using (new StringReader(builder.ToString())) tryReadMessageHeader

    static member private messageHeaderLinesTestCases =
        Seq.map<TestCaseData, TestCaseData> (fun tcd -> tcd.Returns(tcd.Arguments.[0]))
            (HttpMessageTests.messageHeaderTestCases)

    [<TestCaseSource("messageHeaderLinesTestCases")>]
    member this.testReadHeaderLines(lines: seq<string>) =
        let appendLine (sb: StringBuilder) = sb.AppendLine
        let builder = lines |> Seq.fold appendLine (StringBuilder())

        using (new StringReader(builder.ToString())) (readHeaderLines >> Seq.toList)
