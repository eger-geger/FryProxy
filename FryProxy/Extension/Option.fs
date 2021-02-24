module Option

let traverse (options: 'a option seq) =
    use folded =
        options
        |> Seq.scan (Option.map2 List.add) (Some List.empty<'a>)
        |> fun s -> s.GetEnumerator()

    let mutable lastSome = None

    while folded.MoveNext() && Option.isSome (lastSome <- folded.Current; lastSome) do
        ignore()

    lastSome
