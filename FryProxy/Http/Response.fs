[<RequireQualifiedAccess>]
module FryProxy.Http.Response

open FryProxy.Http
open System.IO
open System.Text

type Header = StatusLine MessageHeader

let writePlainText (code: uint16) (body: string) (stream: Stream) =
    let bytes = Encoding.UTF8.GetBytes(body)

    let header =
        Header.Header(
            StatusLine.createDefault code,
            [ (ContentType.TextPlain Encoding.UTF8).ToField()
              { ContentLength = uint64 bytes.LongLength }.ToField() ]
        )

    task {
        do! Message.writeHeader header stream
        do! stream.WriteAsync(bytes, 0, bytes.Length)
    }
