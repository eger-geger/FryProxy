module FryProxy.HttpHeaders

open System
open System.IO
open System.Net.Sockets

type HttpHeader = { name: string; values: list<string> }

let readHttpMessageHeader (stream: NetworkStream) =
    let readLine (reader: UnbufferedStreamReader) =
        match reader.ReadLine() with
        | value when not (String.IsNullOrWhiteSpace(value)) -> Some(value, reader)
        | _ -> None

    using (new UnbufferedStreamReader(stream)) (Seq.unfold readLine)
