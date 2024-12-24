module FryProxy.Http.Hpack.Table

open System
open FryProxy.Http

type Entry = { Field: Field; Size: uint64 }

type DynamicTable = { Entries: Entry List; SizeLimit: uint64 }

let empty = { Entries = List.empty; SizeLimit = 0UL }

let Static =
    [| { Name = ":authority"; Value = String.Empty }
       { Name = ":method"; Value = "GET" }
       { Name = ":method"; Value = "POST" }
       { Name = ":path"; Value = "/" }
       { Name = ":path"; Value = "/index.html" }
       { Name = ":scheme"; Value = "http" }
       { Name = ":scheme"; Value = "https" }
       { Name = ":status"; Value = "200" }
       { Name = ":status"; Value = "204" }
       { Name = ":status"; Value = "206" }
       { Name = ":status"; Value = "304" }
       { Name = ":status"; Value = "400" }
       { Name = ":status"; Value = "404" }
       { Name = ":status"; Value = "500" }
       { Name = "accept-charset"; Value = String.Empty }
       { Name = "accept-encoding"; Value = "gzip, deflate" }
       { Name = "accept-language"; Value = String.Empty }
       { Name = "accept-ranges"; Value = String.Empty }
       { Name = "accept"; Value = String.Empty }
       { Name = "access-control-allow-origin"; Value = String.Empty }
       { Name = "age"; Value = String.Empty }
       { Name = "allow"; Value = String.Empty }
       { Name = "authorization"; Value = String.Empty }
       { Name = "cache-control"; Value = String.Empty }
       { Name = "content-disposition"; Value = String.Empty }
       { Name = "content-encoding"; Value = String.Empty }
       { Name = "content-language"; Value = String.Empty }
       { Name = "content-length"; Value = String.Empty }
       { Name = "content-location"; Value = String.Empty }
       { Name = "content-range"; Value = String.Empty }
       { Name = "content-type"; Value = String.Empty }
       { Name = "cookie"; Value = String.Empty }
       { Name = "date"; Value = String.Empty }
       { Name = "etag"; Value = String.Empty }
       { Name = "expect"; Value = String.Empty }
       { Name = "expires"; Value = String.Empty }
       { Name = "from"; Value = String.Empty }
       { Name = "host"; Value = String.Empty }
       { Name = "if-match"; Value = String.Empty }
       { Name = "if-modified-since"; Value = String.Empty }
       { Name = "if-none-match"; Value = String.Empty }
       { Name = "if-range"; Value = String.Empty }
       { Name = "if-unmodified-since"; Value = String.Empty }
       { Name = "last-modified"; Value = String.Empty }
       { Name = "link"; Value = String.Empty }
       { Name = "location"; Value = String.Empty }
       { Name = "max-forwards"; Value = String.Empty }
       { Name = "proxy-authenticate"; Value = String.Empty }
       { Name = "proxy-authorization"; Value = String.Empty }
       { Name = "range"; Value = String.Empty }
       { Name = "referer"; Value = String.Empty }
       { Name = "refresh"; Value = String.Empty }
       { Name = "retry-after"; Value = String.Empty }
       { Name = "server"; Value = String.Empty }
       { Name = "set-cookie"; Value = String.Empty }
       { Name = "strict-transport-security"; Value = String.Empty }
       { Name = "transfer-encoding"; Value = String.Empty }
       { Name = "user-agent"; Value = String.Empty }
       { Name = "vary"; Value = String.Empty }
       { Name = "via"; Value = String.Empty }
       { Name = "www-authenticate"; Value = String.Empty } |]


let tryItem i (table: DynamicTable) =
    let si = i - 1
    let di = si - Static.Length

    if si < Static.Length then
        ValueSome Static[si]
    elif di < table.Entries.Length then
        ValueSome table.Entries[di].Field
    else
        ValueNone

let entrySize (f: Field) : uint64 =
    32UL + (uint64 f.Value.Length) + (uint64 f.Name.Length)

let inline tableSize (entries: Entry List) = entries |> List.sumBy(_.Size)

let entry fld = { Field = fld; Size = entrySize fld }

let inline resize size (table: DynamicTable) =
    let rec trimFront entries =
        match entries with
        | [] -> []
        | _ :: tail when size >= tableSize tail -> tail
        | _ :: tail -> trimFront tail

    let tbl' =
        if tableSize table.Entries > size then
            { table with Entries = table.Entries |> List.rev |> trimFront |> List.rev }
        else
            table

    { tbl' with SizeLimit = size }


let push (e: Entry) (tbl: DynamicTable) =
    if tbl.SizeLimit < e.Size then
        { tbl with Entries = List.empty }
    else
        { tbl with Entries = e :: tbl.Entries } |> resize tbl.SizeLimit

let inline private entryNotFoundError idx = Error $"field at {idx} does not exist"

let inline private indexedFieldName idx table =
    match tryItem (int idx) table with
    | ValueNone -> entryNotFoundError idx
    | ValueSome field -> Ok field.Name

let inline private resolveLiteralFieldName name table =
    match name with
    | Indexed idx -> indexedFieldName idx table
    | Literal lit -> StringLit.toString lit |> Ok

let inline private literalValueField name litVal =
    { Name = name; Value = StringLit.toString litVal }

let inline private addIndexedField table fields litVal name =
    let fld = literalValueField name litVal
    fld :: fields, table |> push(entry fld)

let inline private addNonIndexedField table fields litVal name =
    let fld = literalValueField name litVal
    fld :: fields, table

let inline runCommand (fields: Field List, table: DynamicTable) cmd : Result<Field List * DynamicTable, string> =
    match cmd with
    | TableSize size -> Ok(fields, resize (uint64 size) table)
    | IndexedField idx ->
        match tryItem (int idx) table with
        | ValueNone -> entryNotFoundError idx
        | ValueSome field -> Ok(field :: fields, table)
    | IndexedLiteralField field ->
        table
        |> resolveLiteralFieldName field.Name
        |> Result.map(addIndexedField table fields field.Value)
    | NonIndexedLiteralField field ->
        table
        |> resolveLiteralFieldName field.Name
        |> Result.map(addNonIndexedField table fields field.Value)
    | NeverIndexedLiteralField field ->
        table
        |> resolveLiteralFieldName field.Name
        |> Result.map(addNonIndexedField table fields field.Value)

[<TailCall>]
let rec runCommandBlock block (fields: Field List, table: DynamicTable) =
    match block with
    | [] -> Ok(List.rev fields, table)
    | head :: tail -> runCommand (fields, table) head |> Result.bind(runCommandBlock tail)

let decodeFields (table: DynamicTable) (octets: byte ReadOnlySpan) : Result<Field List * DynamicTable, string> =
    match Decoder.run Command.decodeBlock octets with
    | Ok commands -> runCommandBlock commands ([], table)
    | Error err -> Error err
