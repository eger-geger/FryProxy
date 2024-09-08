[<RequireQualifiedAccess>]
module FryProxy.Http.Response

open System.Net
open FryProxy.Http
open System.Text
open FryProxy.Http.Fields
open FryProxy.IO


let empty code : ResponseMessage =
    { Header = { StartLine = StatusLine.createDefault(code); Fields = [] }
      Body = Empty }

let emptyStatus (status: HttpStatusCode) = empty(uint16 status)

let plainText code (body: string) : ResponseMessage =
    let bytes = Encoding.UTF8.GetBytes(body)

    { Header =
        { StartLine = StatusLine.createDefault code
          Fields =
            [ (ContentType.TextPlain Encoding.UTF8).ToField()
              { ContentLength = uint64 bytes.LongLength }.ToField() ] }
      Body = Sized(MemoryByteSeq bytes) }

let writePlainText code body = Message.write(plainText code body)
