module FryProxy.Tests.Http.ParseHeaderTests

open System
open System.Buffers
open System.IO
open System.Text
open System.Net.Http
open FryProxy
open FryProxy.Http
open FryProxy.IO
open FryProxy.IO.BufferedParser
open FryProxy.Tests.Constraints
open NUnit.Framework

let validHeaders =
    let tc (lines: string seq) (method: string) (uri, kind) version headers =
        let line =
            RequestLine.create
            <| HttpMethod.Parse(method)
            <| Uri(uri, uriKind = kind)
            <| Version(version)

        let fields = Seq.map ((<||) Field.create) headers

        TestCaseData(lines, ExpectedResult = Header(line, List.ofSeq fields))


    seq {
        yield
            tc [ "POST google.com HTTP/1.1"; "" ]
            <||| ("POST", ("google.com", UriKind.Relative), "1.1")
            <| List.empty

        yield
            tc [ "GET / HTTP/1.1"; "Accept: application/json"; "" ]
            <||| ("GET", ("/", UriKind.Relative), "1.1")
            <| [ "Accept", [ "application/json" ] ]

        yield
            tc [ "GET / HTTP/1.1"; "Accept: "; " application/json"; "" ]
            <||| ("GET", ("/", UriKind.Relative), "1.1")
            <| [ "Accept", [ "application/json" ] ]

        yield
            tc [ "GET / HTTP/1.1"; "Accept: application/json"; "   , application/xml"; "" ]
            <||| ("GET", ("/", UriKind.Relative), "1.1")
            <| [ "Accept", [ "application/json"; "application/xml" ] ]

        yield
            tc
                [ "GET / HTTP/1.1"
                  "Accept: application/json"
                  " , application/xml"
                  "Accept-Encoding: gzip, deflate, br"
                  "" ]
            <||| ("GET", ("/", UriKind.Relative), "1.1")
            <| [ "Accept", [ "application/json"; "application/xml" ]
                 "Accept-Encoding", [ "gzip"; "deflate"; "br" ] ]

        yield
            tc
                [ "GET / HTTP/1.1"
                  "Accept: application/json"
                  " , application/xml"
                  "Accept-Encoding: gzip"
                  " ,deflate ,br"
                  "" ]
            <||| ("GET", ("/", UriKind.Relative), "1.1")
            <| [ "Accept", [ "application/json"; "application/xml" ]
                 "Accept-Encoding", [ "gzip"; "deflate"; "br" ] ]

        yield
            tc
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

[<TestCaseSource(nameof validHeaders)>]
let testSucceeds (lines: seq<string>) =
    let bytes = String.Join(Tokens.CRLF, lines) |> Encoding.ASCII.GetBytes

    task {
        use stream = new MemoryStream(bytes)
        use sharedMemory = MemoryPool<byte>.Shared.Rent(4096)
        let buffer = ReadBuffer(sharedMemory.Memory, stream)

        return! Parser.run buffer Parse.requestHeader
    }

let invalidHeader: string list seq =
    seq {
        yield []
        yield [ "" ]
        yield [ "GET / " ]
        yield [ "Accept: text/html,application/xml" ]
        yield [ "GET / HTTP/1.1 Accept: text/html,application/xml" ]
    }

[<TestCaseSource(nameof invalidHeader)>]
let testFails (lines: seq<string>) =
    let bytes = String.Join(Tokens.CRLF, lines) |> Encoding.ASCII.GetBytes

    use stream = new MemoryStream(bytes)
    use sharedMemory = MemoryPool<byte>.Shared.Rent(4096)
    let buffer = ReadBuffer(sharedMemory.Memory, stream)

    Parse.requestHeader |> Parser.run buffer |> shouldThrowAsync<ParseError>.From
