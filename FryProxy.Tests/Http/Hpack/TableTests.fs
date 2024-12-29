module FryProxy.Tests.Http.Hpack.TableTests

open System.Buffers
open FryProxy.Http
open FryProxy.Http.Hpack

open NUnit.Framework

[<Literal>]
let TableSizeLimit = 0x1000u

let decodeFieldTestCases =
    let emptyTbl = { Table.empty with SizeLimit = TableSizeLimit }

    let customKV = { Field.Name = "custom-key"; Value = "custom-header" }
    let samplePath = { Field.Name = ":path"; Value = "/sample/path" }
    let password = { Field.Name = "password"; Value = "secret" }
    let methodGet = { Field.Name = ":method"; Value = "GET" }

    let table1 = { emptyTbl with Entries = [ { Field = customKV; Size = 55u } ] }

    [ TestCaseData("400a 6375 7374 6f6d 2d6b 6579 0d63 7573 746f 6d2d 6865 6164 6572", emptyTbl)
          .SetName("C.2.1. Literal Header Field with Indexing")
          .Returns(struct ([ FieldPack.Default customKV ], table1))

      TestCaseData("040c 2f73 616d 706c 652f 7061 7468", emptyTbl)
          .SetName("C.2.2. Literal Header Field without Indexing")
          .Returns(struct ([ FieldPack.NotIndexed samplePath ], emptyTbl))

      TestCaseData("1008 7061 7373 776f 7264 0673 6563 7265 74", emptyTbl)
          .SetName("C.2.3. Literal Header Field Never Indexed")
          .Returns(struct ([ FieldPack.NeverIndexed password ], emptyTbl))

      TestCaseData("82", emptyTbl)
          .SetName("C.2.4. Indexed Header Field")
          .Returns(struct ([ FieldPack.Default methodGet ], emptyTbl)) ]

let decodeRequestTestCases =
    let emptyTbl = { Table.empty with SizeLimit = TableSizeLimit }
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

    let r1Table = { emptyTbl with Entries = [ { Field = authority; Size = 57u } ] }

    let r32Fields = r31Fields @ [ FieldPack.Default cacheCtl ]
    let r42Fields = r31Fields @ [ FieldPack.HuffmanCoded cacheCtl ]

    let r2Table =
        { r1Table with Entries = { Field = cacheCtl; Size = 53u } :: r1Table.Entries }

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
        { r2Table with Entries = { Field = customKV; Size = 54u } :: r2Table.Entries }

    [ TestCaseData("8286 8441 0f77 7777 2e65 7861 6d70 6c65 2e63 6f6d", emptyTbl)
          .SetName("C.3.1. First Request")
          .Returns(struct (r31Fields, r1Table))

      TestCaseData("8286 8441 8cf1 e3c2 e5f2 3a6b a0ab 90f4 ff", emptyTbl)
          .SetName("C.4.1. First Request")
          .Returns(struct (r41Fields, r1Table))

      TestCaseData("8286 84be 5808 6e6f 2d63 6163 6865", r1Table)
          .SetName("C.3.2. Second Request")
          .Returns(struct (r32Fields, r2Table))

      TestCaseData("8286 84be 5886 a8eb 1064 9cbf", r1Table)
          .SetName("C.4.2. Second Request")
          .Returns(struct (r42Fields, r2Table))

      TestCaseData("8287 85bf 400a 6375 7374 6f6d 2d6b 6579 0c63 7573 746f 6d2d 7661 6c75 65", r2Table)
          .SetName("C.3.3. Third Request")
          .Returns(struct (r33Fields, r3Table))

      TestCaseData("8287 85bf 4088 25a8 49e9 5ba9 7d7f 8925 a849 e95b b8e8 b4bf", r2Table)
          .SetName("C.4.3. Third Request")
          .Returns(struct (r43Fields, r3Table)) ]

let decodeResponseTestCases =
    let table0 = { Table.empty with SizeLimit = 256u }

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

    (* First Response *)
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
            Entries =
                [ { Field = location; Size = 63u }
                  { Field = date1; Size = 65u }
                  { Field = cacheCtl; Size = 52u }
                  { Field = status302; Size = 42u } ] }

    // Second Response – the (":status", "302") header field is evicted from the dynamic table
    // to free space to allow adding the (":status", "307") header field.
    let raw2 = "4803 3330 37c1 c0bf"
    let huf2 = "4883 640e ffc1 c0bf"

    let fields52 = FieldPack.Default status307 :: fields51.Tail
    let fields62 = FieldPack.HuffmanCoded status307 :: fields51.Tail

    let table2 =
        { table1 with
            Entries = { Field = status307; Size = 42u } :: table1.Entries[..2] }

    // Third Response – several header fields are evicted from the dynamic table.
    let raw3 =
        "88c1 611d 4d6f 6e2c 2032 3120 4f63 7420 3230 3133 2032 303a 3133 3a32 3220 474d 54c0 \
         5a04 677a 6970 7738 666f 6f3d 4153 444a 4b48 514b 425a 584f 5157 454f 5049 5541 5851 \
         5745 4f49 553b 206d 6178 2d61 6765 3d33 3630 303b 2076 6572 7369 6f6e 3d31"

    let huf3 =
        "88c1 6196 d07a be94 1054 d444 a820 0595 040b 8166 e084 a62d 1bff \
         c05a 839b d9ab 77ad 94e7 821d d7f2 e6c7 b335 dfdf cd5b 3960 d5af \
         2708 7f36 72c1 ab27 0fb5 291f 9587 3160 65c0 03ed 4ee5 b106 3d50 07"

    let fields53 =
        [ status200; cacheCtl; date2; location; encoding; cookie ]
        |> List.map FieldPack.Default

    let fields63 =
        [ FieldPack.Default status200
          FieldPack.Default cacheCtl
          FieldPack.HuffmanCoded date2
          FieldPack.Default location
          FieldPack.HuffmanCoded encoding
          FieldPack.HuffmanCoded cookie ]

    let table3 =
        { table2 with
            Entries =
                [ { Field = cookie; Size = 98u }
                  { Field = encoding; Size = 52u }
                  { Field = date2; Size = 65u } ] }

    [ TestCaseData(raw1, table0, TestName = "C.5.1. First Response", ExpectedResult = struct (fields51, table1))
      TestCaseData(huf1, table0, TestName = "C.6.1. First Response", ExpectedResult = struct (fields61, table1))

      TestCaseData(raw2, table1, TestName = "C.5.2. Second Response", ExpectedResult = struct (fields52, table2))
      TestCaseData(huf2, table1, TestName = "C.6.2. Second Response", ExpectedResult = struct (fields62, table2))

      TestCaseData(raw3, table2, TestName = "C.5.3. Third Response", ExpectedResult = struct (fields53, table3))
      TestCaseData(huf3, table2, TestName = "C.6.3. Third Response", ExpectedResult = struct (fields63, table3)) ]

[<TestCaseSource(nameof decodeFieldTestCases)>]
[<TestCaseSource(nameof decodeRequestTestCases)>]
[<TestCaseSource(nameof decodeResponseTestCases)>]
let testDecodeBlock hex table =
    Table.decodeFields table (Hex.decodeSpan hex)
    |> Result.defaultValue([], Table.empty)

let invertTestCase (tc: TestCaseData) =
    let octets, tbl0 = tc.Arguments[0] :?> string, tc.Arguments[1]

    let struct (fields, tbl1) =
        tc.ExpectedResult :?> struct (FieldPack List * DynamicTable)

    TestCaseData(fields, tbl0, TestName = tc.TestName, ExpectedResult = struct (octets, tbl1))

let encodeTestCases =
    decodeFieldTestCases @ decodeRequestTestCases @ decodeResponseTestCases
    |> List.map invertTestCase

[<TestCaseSource(nameof encodeTestCases)>]
let testEncodeBlock fields table =
    use mem = MemoryPool.Shared.Rent()
    let buff = mem.Memory.Span
    
    let struct (l, table') = Table.encodeFields table buff fields

    struct (buff.Slice(0, l).ToArray() |> Hex.encodeArr, table')
