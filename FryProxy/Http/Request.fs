module FryProxy.Http.Request

open System.IO
open FryProxy
open FryProxy.Extension.Tuple

let tryParseHeaders (lines: string seq) =
    let head, tail = lines |> Seq.decompose

    Option.map2
        tuple2
        (head |> Option.bind RequestLine.tryParse)
        (tail
         |> Seq.map Header.tryParse
         |> Option.traverse)

let readHeaders (stream: Stream) =
    using (new UnbufferedStreamReader(stream)) (TextReader.toSeq >> Seq.takeWhile String.isNotBlank)
