namespace FryProxy.Http

open FryProxy.Http

[<Struct>]
type StartLine =
    | Request of request: RequestLine
    | Status of status: StatusLine


[<RequireQualifiedAccess>]
module StartLine =

    let encode line =
        match line with
        | Request line -> line.Encode()
        | Status line -> line.Encode()

type StartLine with

    member line.Encode() = StartLine.encode line
