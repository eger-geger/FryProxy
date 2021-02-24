module Seq

let isNotEmpty source = source |> (Seq.isEmpty >> not)

let decompose (source: 'a seq) =
    match Seq.tryHead source with
    | Some (head) -> Some head, Seq.tail source
    | None -> None, Seq.empty