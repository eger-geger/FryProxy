namespace FryProxy.Http

open System

type HttpMethodType =
    | HEAD = 0uy
    | GET = 1uy
    | POST = 2uy
    | PUT = 3uy
    | DELETE = 4uy
    | TRACE = 5uy
    | CONNECT = 6uy
    | OPTIONS = 7uy

type StartLine = { method: HttpMethodType; uri: Uri; version: Version }

type HttpHeader = { name: string; values: list<string> }

type HttpMessageHeader = { startLine: StartLine; headers: list<HttpHeader> }
