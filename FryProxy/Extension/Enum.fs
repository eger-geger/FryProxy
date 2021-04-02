module System.Enum

let tryParse name = Enum.TryParse(name) |> Option.ofAttempt
