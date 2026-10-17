namespace FryProxy.Http2.Fields

open System
open System.Net.Http
open FryProxy.Extension
open FryProxy.Http.Fields

[<Struct>]
type Authority =
    { Authority: string }

    interface IFieldModel<Authority> with
        static member Name = ":authority"
        
        member this.Encode() = this.Authority

        static member TryDecode v =
            match v with
            | "" -> None
            | v -> Some { Authority = v }

[<Struct>]
type Method =
    { Method: HttpMethod }

    interface IFieldModel<Method> with
        static member Name = ":method"
        
        member this.Encode() = this.Method.Method

        static member TryDecode v =
            try
                Some { Method = HttpMethod.Parse v }
            with _ ->
                None

[<Struct>]
type Path =
    { Path: string }

    interface IFieldModel<Path> with
        static member Name = ":path"
        
        member this.Encode() = this.Path

        static member TryDecode v =
            match v with
            | "" -> None
            | v -> Some { Path = v }

[<Struct>]
type Scheme =
    { Scheme: string }

    [<Literal>]
    static let HttpToken = "http"

    [<Literal>]
    static let HttpsToken = "https"

    static member Http = { Scheme = HttpToken }

    static member Https = { Scheme = HttpsToken }

    interface IFieldModel<Scheme> with
        static member Name = ":scheme"
        
        member this.Encode() = this.Scheme

        static member TryDecode v =
            match v with
            | "" -> None
            | v -> Some { Scheme = v }

[<Struct>]
type Status =
    { Status: uint16 }

    interface IFieldModel<Status> with
        static member Name = ":status"
        
        member this.Encode() = this.Status.ToString()

        static member TryDecode v =
            v |> UInt16.TryParse |> Option.ofAttempt |> Option.map (fun v -> { Status = v })

