module FryProxy.Http.Hpack.Table

open FryProxy.Http

type Entry = { Field: Field; Size: uint64 }

type DynamicTable = { Entries: Entry List; SizeLimit: uint64 }

let emptyTable = { Entries = List.empty; SizeLimit = 0UL }

let staticTable =
    [ { Name = ":authority"; Values = List.Empty }
      { Name = ":method"; Values = [ "GET" ] }
      { Name = ":method"; Values = [ "POST" ] }
      { Name = ":path"; Values = [ "/" ] }
      { Name = ":path"; Values = [ "/index.html" ] }
      { Name = ":scheme"; Values = [ "http" ] }
      { Name = ":scheme"; Values = [ "https" ] }
      { Name = ":status"; Values = [ "200" ] }
      { Name = ":status"; Values = [ "204" ] }
      { Name = ":status"; Values = [ "206" ] }
      { Name = ":status"; Values = [ "304" ] }
      { Name = ":status"; Values = [ "400" ] }
      { Name = ":status"; Values = [ "404" ] }
      { Name = ":status"; Values = [ "500" ] }
      { Name = "accept-charset"; Values = List.Empty }
      { Name = "accept-encoding"; Values = [ "gzip"; "deflate" ] }
      { Name = "accept-language"; Values = List.Empty }
      { Name = "accept-ranges"; Values = List.Empty }
      { Name = "accept"; Values = List.Empty }
      { Name = "access-control-allow-origin"; Values = List.Empty }
      { Name = "age"; Values = List.Empty }
      { Name = "allow"; Values = List.Empty }
      { Name = "authorization"; Values = List.Empty }
      { Name = "cache-control"; Values = List.Empty }
      { Name = "content-disposition"; Values = List.Empty }
      { Name = "content-encoding"; Values = List.Empty }
      { Name = "content-language"; Values = List.Empty }
      { Name = "content-length"; Values = List.Empty }
      { Name = "content-location"; Values = List.Empty }
      { Name = "content-range"; Values = List.Empty }
      { Name = "content-type"; Values = List.Empty }
      { Name = "cookie"; Values = List.Empty }
      { Name = "date"; Values = List.Empty }
      { Name = "etag"; Values = List.Empty }
      { Name = "expect"; Values = List.Empty }
      { Name = "expires"; Values = List.Empty }
      { Name = "from"; Values = List.Empty }
      { Name = "host"; Values = List.Empty }
      { Name = "if-match"; Values = List.Empty }
      { Name = "if-modified-since"; Values = List.Empty }
      { Name = "if-none-match"; Values = List.Empty }
      { Name = "if-range"; Values = List.Empty }
      { Name = "if-unmodified-since"; Values = List.Empty }
      { Name = "last-modified"; Values = List.Empty }
      { Name = "link"; Values = List.Empty }
      { Name = "location"; Values = List.Empty }
      { Name = "max-forwards"; Values = List.Empty }
      { Name = "proxy-authenticate"; Values = List.Empty }
      { Name = "proxy-authorization"; Values = List.Empty }
      { Name = "range"; Values = List.Empty }
      { Name = "referer"; Values = List.Empty }
      { Name = "refresh"; Values = List.Empty }
      { Name = "retry-after"; Values = List.Empty }
      { Name = "server"; Values = List.Empty }
      { Name = "set-cookie"; Values = List.Empty }
      { Name = "strict-transport-security"; Values = List.Empty }
      { Name = "transfer-encoding"; Values = List.Empty }
      { Name = "user-agent"; Values = List.Empty }
      { Name = "vary"; Values = List.Empty }
      { Name = "via"; Values = List.Empty }
      { Name = "www-authenticate"; Values = List.Empty } ]

let tryItem i (table: DynamicTable) =
    if i < staticTable.Length then
        ValueSome staticTable[i]
    elif i < table.Entries.Length then
        ValueSome table.Entries[i].Field
    else
        ValueNone

let entrySize (f: Field) : uint64 =
    let valueLength = f.Values |> List.sumBy(_.Length)
    32UL + (uint64 valueLength) + (uint64 f.Name.Length)

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
