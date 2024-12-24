﻿namespace FryProxy.Http.Fields

open System
open FryProxy.Extension

[<Struct>]
type ContentLength =
    { ContentLength: uint64 }

    interface IFieldModel<ContentLength> with
        static member Name = "Content-Length"

        member this.Encode() = this.ContentLength.ToString()

        static member TryDecode value =
            value
            |> UInt64.TryParse
            |> Option.ofAttempt
            |> Option.map (fun length -> { ContentLength = length })
