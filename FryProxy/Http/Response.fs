[<RequireQualifiedAccess>]
module FryProxy.Http.Response

open FryProxy.Http
open System.Text
open FryProxy.Http.Fields
open FryProxy.IO


let plainText code (body: string) : ResponseMessage =
    let bytes = Encoding.UTF8.GetBytes(body)

    Message(
        Header(
            StatusLine.createDefault code,
            [ (ContentType.TextPlain Encoding.UTF8).ToField()
              { ContentLength = uint64 bytes.LongLength }.ToField() ]
        ),
        Sized(uint64 bytes.LongLength, ReadOnlyMemoryBytes(bytes))
    )

let writePlainText code body = Message.write (plainText code body)
