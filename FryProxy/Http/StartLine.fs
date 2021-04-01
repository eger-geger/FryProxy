namespace FryProxy.Http

open FryProxy.Http

type HttpStartLine =
    | R of HttpRequestLine
    | S of HttpStatusLine

module StartLine =

    let toString line =
        match line with
        | R line -> RequestLine.toString line
        | S line -> StatusLine.toString line
