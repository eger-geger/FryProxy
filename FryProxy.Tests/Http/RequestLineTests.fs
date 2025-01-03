namespace FryProxy.Tests.Http

open System
open System.Net.Http
open NUnit.Framework
open FryProxy.Http
open FsUnit
open FsUnitTyped

type RequestLineTests() =

    static member private samples includeNone =
        let success (line: string) (method: string) uri (version: string) =
            let requestLine =
                RequestLine.create
                <| Version.Parse version
                <| HttpMethod.Parse method
                <| uri

            TestCaseData(line, ValueSome(requestLine))

        let failure (line: string) = TestCaseData(line, None)

        seq {
            if includeNone then
                yield failure ""
                yield failure "GET  "
                yield failure "GET / "
                yield failure "GET / HTTP"
                yield failure "GET \ HTTP"
                yield failure "GET / HTTP/a.a"

            yield success "GET / HTTP/1.0" "GET" "/" "1.0"
            yield success "OPTIONS * HTTP/1.1" "OPTIONS" "*" "1.1"
            yield success "POST /checkout/ HTTP/1.1" "POST" "/checkout/" "1.1"
            yield success "CONNECT google.com:80 HTTP/1.1" "CONNECT" "google.com:80" "1.1"
            yield success "DELETE http://google.com HTTP/1.1" "DELETE" "http://google.com" "1.1"
            yield success "HEAD google.com/search?q=hello+world HTTP/1.1" "HEAD" "google.com/search?q=hello+world" "1.1"
        }

    [<TestCaseSource(nameof RequestLineTests.samples, methodParams = [| true |])>]
    member this.testTryParse(line, requestLineOption) =
        RequestLine.tryDecode line |> shouldEqual requestLineOption

    [<TestCaseSource(nameof RequestLineTests.samples, methodParams = [| false |])>]
    member this.testToString(line, requestLineOption) =
        ValueOption.get requestLineOption |> StartLine.encode |> shouldEqual line

    static member private invalidArguments =
        seq {
            yield TestCaseData(Version(1, 1), HttpMethod.Get, null, typeof<ArgumentNullException>)
            yield TestCaseData(null, HttpMethod.Get, "http://gexample.com", typeof<ArgumentNullException>)
        }

    [<TestCaseSource(nameof RequestLineTests.invalidArguments)>]
    member this.testCreateFailure (method, uri, version) errorType =
        (fun () -> RequestLine.create method uri version |> ignore)
        |> should throw errorType
