module FryProxy.Http.Message

open System.IO
open System.Text

let crlf = [| 0x0Duy; 0x0Auy |]

let serializeHeaders startLine headers =
    let stream = new MemoryStream()
    use writer = new StreamWriter(stream, Encoding.ASCII)

    Seq.map Header.toString headers
    |> Seq.cons (StartLine.toString startLine)
    |> Seq.iter<string> writer.WriteLine

    stream.Write(crlf, 0, 2)
    stream