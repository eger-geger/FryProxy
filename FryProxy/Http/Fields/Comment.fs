module FryProxy.Http.Fields.Comment

/// Matches a comment Field value
let (|Comment|_|) (str: string) =
    if str.StartsWith '(' && str.EndsWith ')' then
        Some(str.Substring(1, str.Length - 1))
    else
        None
