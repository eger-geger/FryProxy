namespace System.IO

open System

module TextReader =

    let tryReadLine (reader: TextReader) = reader.ReadLine() |> Option.ofObj

    let readLines reader = Seq.unfold (tryReadLine >> Option.map (Tuple.append1 reader)) reader
