[<RequireQualifiedAccess>]
module FryProxy.Http.Response

open System.Net
open FryProxy.Http
open System.Text
open FryProxy.Http.Fields
open FryProxy.IO


let empty code : ResponseMessage =
    Message(Header(StatusLine.createDefault(code), List.empty), Empty)

let emptyStatus (status: HttpStatusCode) = empty(uint16 status)

let plainText code (body: string) : ResponseMessage =
    let bytes = Encoding.UTF8.GetBytes(body)

    Message(
        Header(
            StatusLine.createDefault code,
            [ (ContentType.TextPlain Encoding.UTF8).ToField()
              { ContentLength = uint64 bytes.LongLength }.ToField() ]
        ),
        Sized(MemoryByteSeq bytes)
    )

let writePlainText code body = Message.write(plainText code body)
