module FryProxy.Http.Response

open System.IO
open System.Text

let crlf = [| 0x0Duy; 0x0Auy |]

let stringifyHeaders (status, headers) =
    seq {
        yield StatusLine.toString status
        yield! Seq.map Header.toString headers
    }

let serializeHeaders (status, headers) =
    let stream = new MemoryStream()
    use writer = new StreamWriter(stream, Encoding.ASCII)
    let lines = stringifyHeaders (status, headers)
    
    Seq.iter<string> writer.WriteLine lines
    
    stream

let plainText (code: uint16) (body: string) =
    let bytes = Encoding.UTF8.GetBytes(body)
    let statusLine = StatusLine.createStd code

    let headers =
        [ Header.ContentType.textPlain Encoding.UTF8
          Header.ContentLength.create bytes.LongLength ]

    use headerStream = serializeHeaders(statusLine, headers)

    let buffer = new MemoryStream()
    headerStream.WriteTo(buffer)
    buffer.Write(crlf, 0, 2)
    buffer.Write(bytes, 0, bytes.Length)
    
    buffer
