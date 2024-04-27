module Option

let traverse (options: 'a option seq) =
    use folded =
        options
        |> Seq.scan (Option.map2 List.add) (Some List.empty<'a>)
        |> _.GetEnumerator()

    let mutable prev = None

    while folded.MoveNext()
          && Option.isSome (
              prev <- folded.Current
              prev
          ) do
        ()

    prev

let inline ofAttempt (success, value) = if success then Some value else None

/// Evaluate value wrapped in option if condition evaluates to true, otherwise - None.
let inline conditional value condition = ofAttempt (condition, value)
