module FryProxy.Http.Message

open System.IO
open System.Text

/// Asynchronously write message first line and headers to stream.
let writeHeader (startLine, headers: HttpHeader list) (stream: Stream) =
    task {
        use writer = new StreamWriter(stream, Encoding.ASCII, -1, true)

        do! writer.WriteLineAsync(StartLine.toString startLine)

        for h in headers do
            do! writer.WriteLineAsync(HttpHeader.encode h)

        do! writer.WriteLineAsync()
    }
