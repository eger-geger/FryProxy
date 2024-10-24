module FryProxy.Pipeline.Failures

open System.Net
open System.Text

/// Signals about expected failure in request processing pipeline mapped to HTTP status code.
exception PipelineFailure of code: HttpStatusCode * message: string

/// Throw a pipeline failure error with HTTP status code and message send as body.
let inline pipelineFailure code msg = raise(PipelineFailure(code, msg))

/// Throw pipeline failure error with gateway timeout status.
let inline gatewayTimeout operation =
    pipelineFailure HttpStatusCode.GatewayTimeout $"Timed out {operation}"

/// Throw pipeline failure error with bad gateway status.
let inline badGateway msg =
    pipelineFailure HttpStatusCode.BadGateway msg

/// Throw pipeline failure error with service unavailable status.
let inline serviceUnavailable msg =
    pipelineFailure HttpStatusCode.ServiceUnavailable msg

/// Throw pipeline failure error with bad request status.
let inline badRequest msg =
    pipelineFailure HttpStatusCode.BadRequest msg

/// Throw pipeline failure error with request timeout status.
let inline requestTimeout operation =
    pipelineFailure HttpStatusCode.RequestTimeout $"Timed out {operation}"

/// Format an error for HTTP message body. Includes stacktrace for debug builds.
let fmtFailure (operation: string) (err: exn) =
    StringBuilder()
        .AppendLine($"Failed {operation}")
        .Append("\t")
        .AppendLine($"{err.GetType().Name}: {err.Message}")
#if DEBUG
        .AppendLine(err.StackTrace)
#endif
        .ToString()
