[<AutoOpen>]
module FryProxy.Extension.Option

type 'a Option with

    /// Lift boolean flag and value to a Option
    static member inline ofAttempt(success, value) = if success then Some value else None

    /// Convert option to value option
    static member inline toValue(op: Option<_>) =
        match op with
        | Some a -> ValueSome(a)
        | None -> ValueNone

type 'a ValueOption with

    /// Convert ValueOption to regular option
    static member inline toOption vo =
        match vo with
        | ValueSome v -> Some v
        | ValueNone -> None

    /// Lift boolean flag and value to a ValueOption
    static member inline ofAttempt(success, value) =
        if success then ValueSome value else ValueNone

open System

type Uri with

    /// Attempt to parse Uri from string and return result as ValueOption
    static member inline tryParse uri =
        ValueOption.ofAttempt(Uri.TryCreate(uri, UriKind.RelativeOrAbsolute))

type System.Text.RegularExpressions.Regex with

    /// Match string against regex and return ValueOption
    member regex.tryMatch s =
        let m = regex.Match(s)

        if m.Success then ValueSome m else ValueNone
