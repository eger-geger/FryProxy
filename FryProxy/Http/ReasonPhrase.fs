module FryProxy.Http.ReasonPhrase

open System

let forStatusCode (code: uint16) =
    match code with
    | 100us -> "Continue"
    | 101us -> "Switching Protocols"
    | 200us -> "OK"
    | 201us -> "Created"
    | 202us -> "Accepted"
    | 203us -> "Non-Authoritative Information"
    | 204us -> "No Content"
    | 205us -> "Reset Content"
    | 206us -> "Partial Content"
    | 300us -> "Multiple Choices"
    | 301us -> "Moved Permanently"
    | 302us -> "Found"
    | 303us -> "See Other"
    | 304us -> "Not Modified"
    | 305us -> "Use Proxy"
    | 307us -> "Temporary Redirect"
    | 400us -> "Bad Request"
    | 401us -> "Unauthorized"
    | 402us -> "Payment Required"
    | 403us -> "Forbidden"
    | 404us -> "Not Found"
    | 405us -> "Method Not Allowed"
    | 406us -> "Not Acceptable"
    | 407us -> "Proxy Authentication Required"
    | 408us -> "Request Timeout"
    | 409us -> "Conflict"
    | 410us -> "Gone"
    | 411us -> "Length Required"
    | 412us -> "Precondition Failed"
    | 413us -> "Payload Too Large"
    | 414us -> "URI Too Long"
    | 415us -> "Unsupported Media Type"
    | 416us -> "Range Not Satisfiable"
    | 417us -> "Expectation Failed"
    | 426us -> "Upgrade Required"
    | 500us -> "Internal Server Error"
    | 501us -> "Not Implemented"
    | 502us -> "Bad Gateway"
    | 503us -> "Service Unavailable"
    | 504us -> "Gateway Timeout"
    | 505us -> "HTTP Version Not Supported"
    | _ -> raise (ArgumentOutOfRangeException(nameof code))
