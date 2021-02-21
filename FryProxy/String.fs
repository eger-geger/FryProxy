module String

open System

let isNotBlank value = value |> (String.IsNullOrWhiteSpace >> not)

