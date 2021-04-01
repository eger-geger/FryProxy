module System.Text.RegularExpressions.Regex

let tryMatch (regex: Regex) s =
    let a_match = regex.Match(s)

    if a_match.Success then Some a_match else None
