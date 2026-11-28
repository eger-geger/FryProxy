module FryProxy.Http2.Request

open FryProxy.Http

open System

/// Checks for a lowercase field-name token character (RFC 9113 §8.2.1; RFC 9110 §§5.1, 5.6.2).
let inline isTokenChar ch =
    ch = '!'
    || (ch >= '#' && ch <= '\'')
    || (ch >= '*' && ch <= '+')
    || (ch >= '-' && ch <= '.')
    || (ch >= '0' && ch <= '9')
    || (ch >= '^' && ch <= 'z')
    || ch = '|'
    || ch = '~'

/// Checks that a string contains a non-empty token from the given offset onward.
let inline isToken offset (value: string) =
    let rec loop i =
        if i >= value.Length then true
        elif isTokenChar value[i] then loop (i + 1)
        else false

    if isNull value || offset < 0 || offset >= value.Length then
        false
    else
        loop offset

/// Checks a non-null, non-empty lowercase token, allowing a single leading colon for a pseudo-header
/// (RFC 9113 §§8.2.1, 8.3).
let inline isFieldName (name: string) =
    String.IsNullOrEmpty name |> not
    && isToken (if name[0] = ':' then 1 else 0) name

/// Checks for a visible ASCII or obs-text field-value character (RFC 9110 §5.5).
let inline isFieldValueChar ch =
    ch >= '\u0021' && ch <= '\u007E' || ch >= '\u0080' && ch <= '\u00FF'

let inline isFieldValueSpace ch = ch = ' ' || ch = '\t'

/// Accepts empty values or field-value characters with spaces and tabs only internally; rejects null
/// (RFC 9113 §8.2.1; RFC 9110 §5.5).
let inline isFieldValue (value: string) =
    let rec loop left right =
        if left >= right then
            true
        elif isFieldValueChar value[left] || isFieldValueSpace value[left] then
            loop (left + 1) right
        else
            false

    if isNull value then
        false
    elif value.Length = 0 then
        true
    else
        isFieldValueChar value[0]
        && isFieldValueChar value[value.Length - 1]
        && loop 1 (value.Length - 1)

/// Checks for connection-specific field names prohibited by RFC 9113 §8.2.2.
let inline isConnectionField v =
    match v with
    | "connection"
    | "keep-alive"
    | "proxy-connection"
    | "transfer-encoding"
    | "upgrade" -> true
    | _ -> false

let inline private isTrailersValue v =
    String.Equals(v, "trailers", StringComparison.OrdinalIgnoreCase)

/// Validates request field syntax and connection-specific fields, including the TE trailers exception
/// (RFC 9113 §§8.2.1–8.2.2). Returns the original list or the first validation error.
/// Does not check pseudo-header semantics such as ordering, presence, or duplication (RFC 9113 §8.3).
let validateFields (fields: Field list) : Result<Field list, string> =
    let rec validate =
        function
        | [] -> Ok fields
        | field :: _ when not (isFieldName field.Name) ->
            Error "HTTP/2 field names must be lowercase and use token characters."
        | field :: _ when not (isFieldValue field.Value) -> Error "HTTP/2 field values contain invalid characters."
        | field :: _ when isConnectionField field.Name ->
            Error "HTTP/2 messages must not contain connection-specific fields."
        | field :: _ when field.Name = "te" && not (isTrailersValue field.Value) ->
            Error "HTTP/2 TE field value must be trailers."
        | _ :: remaining -> validate remaining

    validate fields
