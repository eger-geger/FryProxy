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
    
module MethodType =
    
    let tryParse method =
        match Enum.TryParse<HttpMethodType> method with
        | true, value -> Some value
        | false, _ -> None

