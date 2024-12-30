module FryProxy.Http.Hpack.Table

open System
open FryProxy.Http
open Microsoft.FSharp.Collections
open Microsoft.FSharp.Core

let empty = { Entries = List.empty; SizeLimit = 0u }

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
    |> Array.AsReadOnly

/// Return table item by global index searching in both static and dynamic tables.
let tryItem i (table: DynamicTable) =
    let si = i - 1
    let di = si - Static.Count

    if si < Static.Count then
        ValueSome Static[si]
    elif di < table.Entries.Length then
        ValueSome table.Entries[di].Field
    else
        ValueNone

/// Attempt to find global index of the first field satisfying the predicate in both static and dynamic tables.
let inline tryFindIndex predicate (tbl: DynamicTable) =
    let dynamicResult () =
        match tbl.Entries |> List.tryFindIndex(_.Field >> predicate) with
        | None -> ValueNone
        | Some i -> ValueSome(Static.Count + i + 1)

    let staticResult =
        match Static |> Seq.tryFindIndex predicate with
        | None -> ValueNone
        | Some i -> ValueSome(i + 1)

    staticResult |> ValueOption.orElseWith(dynamicResult)

/// Attempt to find global index of the first field with a given name in both static and dynamic tables.
let tryFindNameIndex name = tryFindIndex(_.Name >> (=) name)

/// Attempt to find global index of the matching field in both static and dynamic tables.
let tryFindFieldIndex fld = tryFindIndex((=) fld)

/// Compute field table entry size.
let inline entrySize (f: Field) =
    32u + (uint32 f.Value.Length) + (uint32 f.Name.Length)

/// Compute total size of dynamic table entries.
let inline tableSize (entries: TableEntry List) = entries |> List.sumBy(_.Size)

/// Construct table entry from a field.
let entry fld = { Field = fld; Size = entrySize fld }

/// Drop older dynamic table entries until total size of the remaining entries fits into table size limit.
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

/// Add dynamic table entry possibly evicting older entries.
let push (e: TableEntry) (tbl: DynamicTable) =
    if tbl.SizeLimit < e.Size then
        { tbl with Entries = List.Empty }
    else
        { tbl with Entries = e :: tbl.Entries } |> resize tbl.SizeLimit

/// Construct dynamic table entry from a field and add it to the table possibly evicting older entries.
let pushField = entry >> push

let inline private entryNotFoundError idx = Error $"field at {idx} does not exist"

let inline private indexedFieldName idx table =
    match tryItem (int idx) table with
    | ValueNone -> entryNotFoundError idx
    | ValueSome field -> Ok field.Name

let inline private resolveLiteralFieldName name table =
    match name with
    | Indexed idx -> indexedFieldName idx table
    | Literal lit -> StringLit.toString lit |> Ok

let inline private buildLiteralField kind (tbl: DynamicTable) { Name = name; Value = value } =
    match tbl |> tryFindNameIndex name with
    | ValueSome i -> Command.literalIndexedField kind i value
    | ValueNone -> Command.literalStringField kind name value

let inline unpackField opts litVal name =
    let value, opt =
        match litVal with
        | Raw str -> str, opts
        | Huf str -> str, opts ||| PackOpts.HuffmanCoded

    FieldPack({ Name = name; Value = value }, opt)

/// Execute a command updating field list, dynamic table or both.
let inline runCommand struct (fields: FieldPack List, tbl: DynamicTable) cmd =
    match cmd with
    | TableSize size -> Ok <| struct (fields, resize size tbl)
    | IndexedField idx ->
        match tryItem (int idx) tbl with
        | ValueNone -> entryNotFoundError idx
        | ValueSome field -> Ok <| struct (FieldPack.Default field :: fields, tbl)
    | IndexedLiteralField(name, value) ->
        tbl
        |> resolveLiteralFieldName name
        |> Result.map(unpackField PackOpts.RawIndexed value)
        |> Result.map(fun (FieldPack(fld, _) as fp) -> fp :: fields, pushField fld tbl)
    | NonIndexedLiteralField(name, value) ->
        tbl
        |> resolveLiteralFieldName name
        |> Result.map(unpackField PackOpts.NotIndexed value)
        |> Result.map(fun fp -> fp :: fields, tbl)
    | NeverIndexedLiteralField(name, value) ->
        tbl
        |> resolveLiteralFieldName name
        |> Result.map(unpackField PackOpts.NeverIndexed value)
        |> Result.map(fun fp -> fp :: fields, tbl)

/// Create serializable command from a given field and dynamic table.
let buildCommand (FieldPack(fld, opts)) (tbl: DynamicTable) =
    let lit =
        if opts.HasFlag(PackOpts.HuffmanCoded) then
            Huf
        else
            Raw

    if opts.HasFlag(PackOpts.NeverIndexed) then
        NeverIndexedLiteralField <| buildLiteralField lit tbl fld
    elif opts.HasFlag(PackOpts.NotIndexed) then
        NonIndexedLiteralField <| buildLiteralField lit tbl fld
    else
        match tbl |> tryFindFieldIndex fld with
        | ValueSome i -> Command.indexedField i
        | ValueNone -> IndexedLiteralField <| buildLiteralField lit tbl fld

[<TailCall>]
let rec private runCommandBlock struct (fields, table) commands =
    match commands with
    | [] -> Ok struct (List.rev fields, table)
    | head :: tail ->
        match runCommand (fields, table) head with
        | Ok acc -> runCommandBlock acc tail
        | Error err -> Error err

[<TailCall>]
let rec private buildCommandBlock fields tbl acc =
    match fields with
    | [] -> struct (List.rev acc, tbl)
    | FieldPack(fld, _) as head :: tail ->
        match buildCommand head tbl with
        | IndexedLiteralField _ as cmd -> buildCommandBlock tail (pushField fld tbl) (cmd :: acc)
        | cmd -> buildCommandBlock tail tbl (cmd :: acc)

/// Decode packet fields from buffer modifying dynamic table in the process.
/// Returns list of decoded packed fields and updated dynamic table.
let decodeFields table octets =
    Decoder.run Command.decodeBlock octets
    |> Result.bind(runCommandBlock([], table))

/// Encode fields into buffer modifying dynamic table in the process.
/// Returns number of bytes written in buffer and updated dynamic table.
let encodeFields table buffer fields =
    let struct (commands, tbl') = buildCommandBlock fields table []
    struct (Command.encodeBlock commands buffer, tbl')
