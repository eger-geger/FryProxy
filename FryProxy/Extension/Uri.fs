module System.Uri

let tryParse uri =
    Option.ofAttempt (Uri.TryCreate(uri, UriKind.RelativeOrAbsolute))
