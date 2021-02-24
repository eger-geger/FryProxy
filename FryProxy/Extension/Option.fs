module Option

let tryHead (options: 'a option seq) = options |> Seq.tryHead |> Option.flatten

let traverse (options: 'a option seq) =
    use folded =
        options
        |> Seq.scan (Option.map2 List.add) (Some List.empty<'a>)
        |> fun s -> s.GetEnumerator()

    let mutable lastSome = None
    let mutable isNotEmpty = folded.MoveNext()

    while isNotEmpty && Option.isSome folded.Current do
        lastSome <- folded.Current
        isNotEmpty <- folded.MoveNext()

    if isNotEmpty then None else lastSome
