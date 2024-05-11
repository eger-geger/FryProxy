module FryProxy.Http.Response

open System.IO
open System.Text

let writePlainText (code: uint16) (body: string) (stream: Stream) =
    let bytes = Encoding.UTF8.GetBytes(body)
    let statusLine = StatusLine.createDefault code

    let headers =
        [ (ContentType.TextPlain Encoding.UTF8).ToField()
          { ContentLength = uint64 bytes.LongLength }.ToField() ]

    task {
        do! Message.writeHeader (Status statusLine, headers) stream
        do! stream.WriteAsync(bytes, 0, bytes.Length)
    }
