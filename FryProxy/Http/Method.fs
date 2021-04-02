namespace FryProxy.Http

open System

type HttpMethod =
    | HEAD = 0uy
    | GET = 1uy
    | POST = 2uy
    | PUT = 3uy
    | DELETE = 4uy
    | TRACE = 5uy
    | CONNECT = 6uy
    | OPTIONS = 7uy

