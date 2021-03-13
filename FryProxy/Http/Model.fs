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

type HttpRequestLine = { method: HttpMethodType; uri: Uri; version: Version }
type HttpStatusLine = { version: Version; code: uint16; text: string }
type HttpHeader = { name: string; values: string list }
