namespace FryProxy.Http

open System
open System.Text

[<Struct>]
type Host =
    { Host: string }

    static member Name = "Host"

    member this.Encode() = [ this.Host ]

    static member TryDecode values =
        List.tryExactlyOne values |> Option.map (fun host -> { Host = host })

[<Struct>]
type ContentLength =
    { ContentLength: uint64 }

    static member Name = "Content-Length"

    member this.Encode() = [ this.ContentLength.ToString() ]

    static member TryDecode(values: string list) =
        values
        |> List.tryExactlyOne
        |> Option.bind (UInt64.TryParse >> Option.ofAttempt)
        |> Option.map (fun length -> { ContentLength = length })


[<Struct>]
type ContentType =
    { Values: string list }

    static member Name = "Content-Type"

    member this.Encode() = this.Values

    static member TryDecode values = { Values = values }
    
    static member TextPlain(enc: Encoding) =
        { Values = [ $"text/plain; encoding={enc.BodyName}" ] }
