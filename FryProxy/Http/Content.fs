module FryProxy.Http.Content

open System.IO

let writeStringAsync(content: string)(writer: StreamWriter) =
    writer.WriteAsync(content) |> Async.AwaitTask
    