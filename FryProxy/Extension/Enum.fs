module System.Enum

let tryParse (name: char ReadOnlySpan) = Enum.TryParse(name) |> Option.ofAttempt
