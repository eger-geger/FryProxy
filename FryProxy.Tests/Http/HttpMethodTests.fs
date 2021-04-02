namespace FryProxy.Tests.Http

open System
open FryProxy.Http
open NUnit.Framework
open FsUnit

type HttpMethodTests() =

    static member private methods = [| "HEAD"; "GET"; "POST"; "PUT"; "DELETE"; "CONNECT"; "TRACE"; "OPTIONS" |]

    [<TestCaseSource(nameof HttpMethodTests.methods)>]
    member this.testParseMethod name =
        let methodOpt: HttpMethod option = Enum.tryParse name
        
        methodOpt |> should be (not' (equal None))
