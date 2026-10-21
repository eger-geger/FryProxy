module FryProxy.Tests.Http2.Hpack.EncodeFieldsTests

open System
open FryProxy.Http
open FryProxy.Http2.Hpack
open FryProxy.Tests.Http2.Hpack
open NUnit.Framework
open FsUnit

[<Literal>]
let DefaultTableSize = 4096u

[<Literal>]
let SmallTableSize = 256u

let emptyDefaultTable = { Table.empty with SizeLimit = DefaultTableSize }

let rfcC2SingleFieldCases =
    let customKV = { Field.Name = "custom-key"; Value = "custom-header" }
    let samplePath = { Field.Name = ":path"; Value = "/sample/path" }
    let password = { Field.Name = "password"; Value = "secret" }
    let methodGet = { Field.Name = ":method"; Value = "GET" }

    let tableWithCustomKV =
        { emptyDefaultTable with
            Size = 55u
            Entries = [ { Field = customKV; Size = 55u } ] }

    [ TestCaseData(
          [ FieldPack.Default customKV ],
          emptyDefaultTable,
          "400a 6375 7374 6f6d 2d6b 6579 0d63 7573 746f 6d2d 6865 6164 6572",
          tableWithCustomKV
      )
          .SetName("C.2.1 Literal Header Field with Indexing")

      TestCaseData(
          [ FieldPack.NotIndexed samplePath ],
          emptyDefaultTable,
          "040c 2f73 616d 706c 652f 7061 7468",
          emptyDefaultTable
      )
          .SetName("C.2.2 Literal Header Field without Indexing")

      TestCaseData(
          [ FieldPack.NeverIndexed password ],
          emptyDefaultTable,
          "1008 7061 7373 776f 7264 0673 6563 7265 74",
          emptyDefaultTable
      )
          .SetName("C.2.3 Literal Header Field Never Indexed")

      TestCaseData([ FieldPack.Default methodGet ], emptyDefaultTable, "82", emptyDefaultTable)
          .SetName("C.2.4 Indexed Header Field") ]

let rfcC3C4RequestCases =
    let authority = { Field.Name = ":authority"; Value = "www.example.com" }
    let cacheCtl = { Field.Name = "cache-control"; Value = "no-cache" }
    let customKV = { Field.Name = "custom-key"; Value = "custom-value" }

    let r31Fields =
        [ FieldPack.Default { Field.Name = ":method"; Value = "GET" }
          FieldPack.Default { Field.Name = ":scheme"; Value = "http" }
          FieldPack.Default { Field.Name = ":path"; Value = "/" }
          FieldPack.Default authority ]

    let r41Fields =
        [ FieldPack.Default { Field.Name = ":method"; Value = "GET" }
          FieldPack.Default { Field.Name = ":scheme"; Value = "http" }
          FieldPack.Default { Field.Name = ":path"; Value = "/" }
          FieldPack.HuffmanCoded authority ]

    let r1Table =
        { emptyDefaultTable with
            Size = 57u
            Entries = [ { Field = authority; Size = 57u } ] }

    let r32Fields = r31Fields @ [ FieldPack.Default cacheCtl ]
    let r42Fields = r31Fields @ [ FieldPack.HuffmanCoded cacheCtl ]

    let r2Table =
        { r1Table with
            Size = 110u
            Entries = { Field = cacheCtl; Size = 53u } :: r1Table.Entries }

    let r33Fields =
        [ FieldPack.Default { Field.Name = ":method"; Value = "GET" }
          FieldPack.Default { Field.Name = ":scheme"; Value = "https" }
          FieldPack.Default { Field.Name = ":path"; Value = "/index.html" }
          FieldPack.Default authority
          FieldPack.Default customKV ]

    let r43Fields =
        [ FieldPack.Default { Field.Name = ":method"; Value = "GET" }
          FieldPack.Default { Field.Name = ":scheme"; Value = "https" }
          FieldPack.Default { Field.Name = ":path"; Value = "/index.html" }
          FieldPack.Default authority
          FieldPack.HuffmanCoded customKV ]

    let r3Table =
        { r2Table with
            Size = 164u
            Entries = { Field = customKV; Size = 54u } :: r2Table.Entries }

    [ TestCaseData(r31Fields, emptyDefaultTable, "8286 8441 0f77 7777 2e65 7861 6d70 6c65 2e63 6f6d", r1Table)
          .SetName("C.3.1 First Request (Raw)")

      TestCaseData(r41Fields, emptyDefaultTable, "8286 8441 8cf1 e3c2 e5f2 3a6b a0ab 90f4 ff", r1Table)
          .SetName("C.4.1 First Request (Huffman)")

      TestCaseData(r32Fields, r1Table, "8286 84be 5808 6e6f 2d63 6163 6865", r2Table)
          .SetName("C.3.2 Second Request (Raw)")

      TestCaseData(r42Fields, r1Table, "8286 84be 5886 a8eb 1064 9cbf", r2Table)
          .SetName("C.4.2 Second Request (Huffman)")

      TestCaseData(
          r33Fields,
          r2Table,
          "8287 85bf 400a 6375 7374 6f6d 2d6b 6579 0c63 7573 746f 6d2d 7661 6c75 65",
          r3Table
      )
          .SetName("C.3.3 Third Request (Raw)")

      TestCaseData(r43Fields, r2Table, "8287 85bf 4088 25a8 49e9 5ba9 7d7f 8925 a849 e95b b8e8 b4bf", r3Table)
          .SetName("C.4.3 Third Request (Huffman)") ]

let rfcC5C6ResponseCases =
    let table0 = { Table.empty with SizeLimit = SmallTableSize }

    let location = { Field.Name = "location"; Value = "https://www.example.com" }
    let date1 = { Field.Name = "date"; Value = "Mon, 21 Oct 2013 20:13:21 GMT" }
    let date2 = { Field.Name = "date"; Value = "Mon, 21 Oct 2013 20:13:22 GMT" }
    let cacheCtl = { Field.Name = "cache-control"; Value = "private" }
    let encoding = { Field.Name = "content-encoding"; Value = "gzip" }

    let cookie =
        { Field.Name = "set-cookie"
          Value = "foo=ASDJKHQKBZXOQWEOPIUAXQWEOIU; max-age=3600; version=1" }

    let status200 = { Field.Name = ":status"; Value = "200" }
    let status302 = { Field.Name = ":status"; Value = "302" }
    let status307 = { Field.Name = ":status"; Value = "307" }

    let raw1 =
        "4803 3330 3258 0770 7269 7661 7465 611d 4d6f 6e2c 2032 3120 4f63 7420 3230 3133 2032 303a \
         3133 3a32 3120 474d 546e 1768 7474 7073 3a2f 2f77 7777 2e65 7861 6d70 6c65 2e63 6f6d"

    let huf1 =
        "4882 6402 5885 aec3 771a 4b61 96d0 7abe 9410 54d4 44a8 2005 9504 0b81 66e0 82a6 2d1b ff6e \
         919d 29ad 1718 63c7 8f0b 97c8 e9ae 82ae 43d3"

    let fields1 = [ status302; cacheCtl; date1; location ]
    let fields51 = fields1 |> List.map FieldPack.Default
    let fields61 = fields1 |> List.map FieldPack.HuffmanCoded

    let table1 =
        { table0 with
            Size = 222u
            Entries =
                [ { Field = location; Size = 63u }
                  { Field = date1; Size = 65u }
                  { Field = cacheCtl; Size = 52u }
                  { Field = status302; Size = 42u } ] }

    let raw2 = "4803 3330 37c1 c0bf"
    let huf2 = "4883 640e ffc1 c0bf"

    let fields52 = FieldPack.Default status307 :: fields51.Tail
    let fields62 = FieldPack.HuffmanCoded status307 :: fields51.Tail

    let table2 =
        { table1 with
            Size = 222u
            Entries = { Field = status307; Size = 42u } :: table1.Entries[..2] }

    let raw3 =
        "88c1 611d 4d6f 6e2c 2032 3120 4f63 7420 3230 3133 2032 303a 3133 3a32 3220 474d 54c0 \
         5a04 677a 6970 7738 666f 6f3d 4153 444a 4b48 514b 425a 584f 5157 454f 5049 5541 5851 \
         5745 4f49 553b 206d 6178 2d61 6765 3d33 3630 303b 2076 6572 7369 6f6e 3d31"

    let huf3 =
        "88c1 6196 d07a be94 1054 d444 a820 0595 040b 8166 e084 a62d 1bff \
         c05a 839b d9ab 77ad 94e7 821d d7f2 e6c7 b335 dfdf cd5b 3960 d5af \
         2708 7f36 72c1 ab27 0fb5 291f 9587 3160 65c0 03ed 4ee5 b106 3d50 07"

    let fields53 =
        ([ status200; cacheCtl; date2; location; encoding ] |> List.map FieldPack.Default)
        @ [ FieldPack.RawIndexed cookie ]

    let fields63 =
        [ FieldPack.Default status200
          FieldPack.Default cacheCtl
          FieldPack.HuffmanCoded date2
          FieldPack.Default location
          FieldPack.HuffmanCoded encoding
          FieldPack.HuffmanCoded cookie ]

    let table3 =
        { table2 with
            Size = 215u
            Entries =
                [ { Field = cookie; Size = 98u }
                  { Field = encoding; Size = 52u }
                  { Field = date2; Size = 65u } ] }

    [ TestCaseData(fields51, table0, raw1, table1).SetName("C.5.1 First Response (Raw)")
      TestCaseData(fields61, table0, huf1, table1).SetName("C.6.1 First Response (Huffman)")
      TestCaseData(fields52, table1, raw2, table2).SetName("C.5.2 Second Response (Raw with eviction)")
      TestCaseData(fields62, table1, huf2, table2).SetName("C.6.2 Second Response (Huffman with eviction)")
      TestCaseData(fields53, table2, raw3, table3).SetName("C.5.3 Third Response (Raw with multiple evictions)")
      TestCaseData(fields63, table2, huf3, table3).SetName("C.6.3 Third Response (Huffman with multiple evictions)") ]

let allRfcEncodeCases =
    rfcC2SingleFieldCases @ rfcC3C4RequestCases @ rfcC5C6ResponseCases

[<TestCaseSource(nameof allRfcEncodeCases)>]
let testRfcEncodeFields
    (fields: FieldPack list, initialTable: DynamicTable, expectedHex: string, expectedTable: DynamicTable)
    =
    let buffer = Array.zeroCreate 4096

    let struct (written, updatedTable, remainder) =
        Table.encodeFields initialTable fields (buffer.AsSpan())

    remainder |> should be Empty
    let actualHex = Hex.encodeArr buffer[.. written - 1]

    actualHex |> should equal expectedHex
    updatedTable.Size |> should equal expectedTable.Size
    updatedTable.Entries |> should equal expectedTable.Entries

let bufferCapacityTestCases =
    seq {
        yield TestCaseData(0, 0, 4, 0).SetName("0 bytes (none encoded)")
        yield TestCaseData(5, 0, 4, 0).SetName("5 bytes (insufficient for 6-byte numeric capacity)")
        yield TestCaseData(6, 1, 3, 1).SetName("6 bytes (first field encoded)")
        yield TestCaseData(7, 2, 2, 2).SetName("7 bytes (first two fields encoded)")
        yield TestCaseData(10, 2, 2, 2).SetName("10 bytes (first two encoded)")
        yield TestCaseData(20, 2, 2, 2).SetName("20 bytes (first two encoded)")
        yield TestCaseData(24, 3, 1, 14).SetName("24 bytes (first three encoded)")
        yield TestCaseData(1024, 4, 0, 27).SetName("1024 bytes (all fields encoded)")
    }

[<TestCaseSource(nameof bufferCapacityTestCases)>]
let testBufferCapacityBoundaries
    (bufferSize: int, expectedEncodedCount: int, expectedRemainingCount: int, expectedWritten: int)
    =
    let fields =
        [ FieldPack.Default { Field.Name = ":method"; Value = "GET" }
          FieldPack.Default { Field.Name = ":scheme"; Value = "https" }
          FieldPack.Default { Field.Name = ":path"; Value = "/resources" }
          FieldPack.Default { Field.Name = ":authority"; Value = "example.com" } ]

    let buffer = Array.zeroCreate bufferSize

    let struct (written, _, remainder) =
        Table.encodeFields emptyDefaultTable fields (buffer.AsSpan())

    written |> should equal expectedWritten
    remainder.Length |> should equal expectedRemainingCount
    fields.Length - remainder.Length |> should equal expectedEncodedCount

[<Test>]
let testEmptyFieldsListEncoding () =
    let buffer = Array.zeroCreate 64

    let struct (written, updatedTable, remainder) =
        Table.encodeFields emptyDefaultTable [] (buffer.AsSpan())

    written |> should equal 0
    updatedTable |> should equal emptyDefaultTable
    remainder |> should be Empty

let roundTripTestCases =
    let headers1 =
        [ { Field.Name = ":method"; Value = "GET" }
          { Field.Name = ":scheme"; Value = "https" }
          { Field.Name = ":path"; Value = "/api/v1/users" }
          { Field.Name = ":authority"; Value = "api.example.com" }
          { Field.Name = "accept"; Value = "application/json" }
          { Field.Name = "user-agent"; Value = "FryProxy/1.0" } ]

    let headers2 =
        [ { Field.Name = ":status"; Value = "200" }
          { Field.Name = "content-type"; Value = "application/json; charset=utf-8" }
          { Field.Name = "cache-control"; Value = "no-cache, no-store" }
          { Field.Name = "x-request-id"; Value = "req-9876543210" } ]

    let headers3 =
        [ { Field.Name = ":method"; Value = "POST" }
          { Field.Name = ":scheme"; Value = "http" }
          { Field.Name = ":path"; Value = "/upload" }
          { Field.Name = "content-length"; Value = "1048576" }
          { Field.Name = "content-type"; Value = "application/octet-stream" }
          { Field.Name = "authorization"; Value = "Bearer test-token-123" }
          { Field.Name = "cookie"; Value = "session=xyz123" } ]

    [ TestCaseData(headers1).SetName("Roundtrip: GET Request Headers")
      TestCaseData(headers2).SetName("Roundtrip: Response Headers")
      TestCaseData(headers3).SetName("Roundtrip: POST Request with Sensitive Headers") ]

[<TestCaseSource(nameof roundTripTestCases)>]
let testEncodeDecodeRoundTrip (rawFields: Field list) =
    let packedFields = rawFields |> List.map FieldPack.Default
    let buffer = Array.zeroCreate 4096

    let struct (written, encTable, rem) =
        Table.encodeFields emptyDefaultTable packedFields (buffer.AsSpan())

    rem |> should be Empty
    written |> should be (greaterThan 0)

    let encodedSpan = ReadOnlySpan(buffer, 0, written)

    match Table.decodeFields emptyDefaultTable encodedSpan with
    | Ok struct (decodedPacked, decTable) ->
        let decodedRaw = decodedPacked |> List.map (fun (FieldPack(f, _)) -> f)
        decodedRaw |> should equal rawFields
        decTable.Size |> should equal encTable.Size
        decTable.Entries |> should equal encTable.Entries
    | Error err -> Assert.Fail($"Failed to decode encoded bytes: {err}")

[<Test>]
let testEncodeRawFieldsDefault () =
    let rawFields =
        [ { Field.Name = ":method"; Value = "GET" }
          { Field.Name = ":path"; Value = "/" } ]

    let buffer = Array.zeroCreate 128

    let struct (written, updatedTable, remainder) =
        Table.encodeFieldsDefault emptyDefaultTable rawFields (buffer.AsSpan())

    written |> should equal 2 // 0x82 0x84
    remainder |> should be Empty
    Hex.encodeArr buffer[.. written - 1] |> should equal "8284"
    updatedTable.Entries |> should be Empty

[<Test>]
let testEncodeFieldsMem () =
    let packedFields =
        [ FieldPack.Default { Field.Name = ":method"; Value = "GET" }
          FieldPack.Default { Field.Name = ":path"; Value = "/" } ]

    let mem, updatedTable = Table.encodeFieldsMem emptyDefaultTable packedFields
    mem.Length |> should equal 2
    Hex.encodeArr (mem.ToArray()) |> should equal "8284"
    updatedTable.Entries |> should be Empty

[<Test>]
let testEncodeRawFieldsMem () =
    let rawFields =
        [ { Field.Name = ":method"; Value = "GET" }
          { Field.Name = ":path"; Value = "/" } ]

    let mem, updatedTable = Table.encodeRawFieldsMem emptyDefaultTable rawFields
    mem.Length |> should equal 2
    Hex.encodeArr (mem.ToArray()) |> should equal "8284"
    updatedTable.Entries |> should be Empty
