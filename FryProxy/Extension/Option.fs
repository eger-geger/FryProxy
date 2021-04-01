module Option

let traverse (options: 'a option seq) =
    use folded =
        options
        |> Seq.scan (Option.map2 List.add) (Some List.empty<'a>)
        |> fun s -> s.GetEnumerator()

    let mutable prev = None

    while folded.MoveNext() && Option.isSome (prev <- folded.Current; prev) do
        ()

    prev
    
let ofAttempt (success, value) =
    if success then Some value
    else None
