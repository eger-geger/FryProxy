module FryProxy.Http.Response

open System.Text

let plainText (code: uint16) (body: string) =
    let bytes = Encoding.UTF8.GetBytes(body)
    let statusLine = StatusLine.createDefault code

    let headers =
        [ Header.ContentType.textPlain Encoding.UTF8
          Header.ContentLength.create bytes.LongLength ]

    use stream = Message.serializeHeaders (S statusLine) headers
    stream.Write(bytes, 0, bytes.Length)
    stream
