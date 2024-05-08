namespace FryProxy.Tests

open System
open System.Buffers
open System.IO
open System.Text
open System.Net.Http
open FryProxy
open FryProxy.Http
open FryProxy.IO
open FryProxy.IO.BufferedParser
open NUnit.Framework

type HttpRequestTests() =

    static member private requestHeaderTestCases =
        let success (lines: string seq) (method: string) (uri, kind) version headers =
            let requestLine =
                RequestLine.create
                <| HttpMethod.Parse(method)
                <| Uri(uri, uriKind = kind)
                <| Version(version)

            let httpHeaders = Seq.map ((<||) HttpHeader.create) headers

            TestCaseData(lines, ExpectedResult = Some(requestLine, List.ofSeq httpHeaders))

        let failure (lines: string seq) =
            TestCaseData(lines, ExpectedResult = None)

        seq {
            yield failure []
            yield failure [ "" ]
            yield failure [ "Accept: application/json" ]
            yield failure [ "POST google.com HTTP/1.1" ]
            yield failure [ "POST google.com HTTP/1.1"; "Accept: application/json" ]

            yield
                success [ "POST google.com HTTP/1.1"; "" ]
                <||| ("POST", ("google.com", UriKind.Relative), "1.1")
                <| List.empty

            yield
                success [ "GET / HTTP/1.1"; "Accept: application/json"; "" ]
                <||| ("GET", ("/", UriKind.Relative), "1.1")
                <| [ "Accept", [ "application/json" ] ]

            yield
                success
                <| [ "GET https://google.com/ HTTP/1.1"
                     "Accept: text/html,application/xml"
                     "Accept-Encoding: gzip, deflate, br"
                     "User-Agent: Chrome/88.0.4324.146"
                     "" ]
                <||| ("GET", ("https://google.com", UriKind.RelativeOrAbsolute), "1.1")
                <| [ "Accept", [ "text/html"; "application/xml" ]
                     "Accept-Encoding", [ "gzip"; "deflate"; "br" ]
                     "User-Agent", [ "Chrome/88.0.4324.146" ] ]
        }

    [<TestCaseSource("requestHeaderTestCases")>]
    member _.testReadHttpRequestHeaders(lines: seq<string>) =
        let appendLine (sb: StringBuilder) (line: string) = sb.AppendLine line
        let builder = lines |> Seq.fold appendLine (StringBuilder())

        task {
            use stream = new MemoryStream(Encoding.ASCII.GetBytes(builder.ToString()))
            use sharedMemory = MemoryPool<byte>.Shared.Rent(4096)
            let buffer = ReadBuffer(sharedMemory.Memory, stream)

            return! Parser.run Parse.requestHeader buffer
        }
