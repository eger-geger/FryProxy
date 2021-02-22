module Option

let tryHead (options: 'a option seq) = options |> Seq.tryHead |> Option.flatten

let traverse (options: 'a option seq) =
    let append list item = list @ [item]
    let folded = Seq.scan (Option.map2 append) (Some List.empty<'a>) options
    
    let mutable head = tryHead folded
    let mutable tail = Seq.tail folded
    
    while (Option.isSome head) && not (Seq.isEmpty tail) do
        head <- tryHead tail
        tail <- Seq.tail tail
        
    head
