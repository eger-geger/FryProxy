module FryProxy.Http.Response

open System.IO
open System.Text

let writePlainText (code: uint16) (body: string) (stream: Stream) =
    let bytes = Encoding.UTF8.GetBytes(body)
    let statusLine = StatusLine.createDefault code

    let headers =
        [ ContentType.textPlain Encoding.UTF8; ContentLength.create bytes.LongLength ]

    task {
        do! Message.writeHeader (Status statusLine, headers) stream
        do! stream.WriteAsync(bytes, 0, bytes.Length)
    }
