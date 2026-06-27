[<AutoOpen>]
module FryProxy.Extension.List

type 'a List with

    // Construct a new list with the first item matching a given predicate replaced with a new value.
    static member replaceFirst selector = List.replaceFirstLoop selector []

    [<TailCall>]
    static member private replaceFirstLoop selector prefix suffix =
        match suffix with
        | [] -> []
        | head :: tail ->
            match selector head with
            | ValueSome v -> List.rev prefix @ v :: tail
            | ValueNone -> List.replaceFirstLoop selector (head :: prefix) tail


    // Returns the first element satisfying the given predicate or ValueNone.
    static member tryFindV predicate items =
        match items with
        | [] -> ValueNone
        | head :: tail ->
            if predicate head then
                ValueSome head
            else
                List.tryFindV predicate tail

