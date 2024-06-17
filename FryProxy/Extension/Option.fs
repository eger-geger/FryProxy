module Option

/// Convert TryXxx function result to option
let inline ofAttempt (success, value) = if success then Some value else None

/// Evaluate value wrapped in option if condition evaluates to true, otherwise - None.
let inline conditional value condition = ofAttempt(condition, value)

/// Convert option to value option
let inline toValue (op: Option<_>) =
    match op with
    | Some a -> ValueSome(a)
    | None -> ValueNone
