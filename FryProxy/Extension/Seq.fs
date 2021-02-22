module Seq

let decompose seq =
    match Seq.tryHead seq with
    | Some(head) -> Some(head), Seq.tail seq
    | _ -> None, Seq.empty
