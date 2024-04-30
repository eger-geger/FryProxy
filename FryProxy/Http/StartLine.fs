namespace FryProxy.Http

open FryProxy.Http

type HttpStartLine =
    | Request of RequestLine
    | Status of HttpStatusLine

module StartLine =

    let toString line =
        match line with
        | Request line -> RequestLine.toString line
        | Status line -> StatusLine.toString line
