module FryProxy.Tests.Http.Hpack.TableTests

open FryProxy.Http
open FryProxy.Http.Hpack

open NUnit.Framework

[<Literal>]
let TableSizeLimit = 0xffffUL

let fieldsAndTable fields entries =
    fields, { Table.Entries = entries; Table.SizeLimit = TableSizeLimit }

let decodeFieldsTestCases =
    let emptyTbl = { Table.empty with SizeLimit = TableSizeLimit }

    let customHeaderFld = { Name = "custom-key"; Values = [ "custom-header" ] }
    let customValueFld = { Name = "custom-key"; Values = [ "custom-value" ] }
    let samplePathFld = { Name = ":path"; Values = [ "/sample/path" ] }
    let pwdFld = { Name = "password"; Values = [ "secret" ] }

    let rootPathFld = { Name = ":path"; Values = [ "/" ] }
    let methodGetFld = { Name = ":method"; Values = [ "GET" ] }
    let schemeHttpFld = { Name = ":scheme"; Values = [ "http" ] }
    let authorityFld = { Name = ":authority"; Values = [ "www.example.com" ] }
    let noCacheFld = { Name = "cache-control"; Values = [ "no-cache" ] }

    let c4Fields = [ methodGetFld; schemeHttpFld; rootPathFld; authorityFld ]
    let c41Table = { emptyTbl with Entries = [ { Field = authorityFld; Size = 57UL } ] }

    let c42Table =
        { c41Table with
            Entries = { Field = noCacheFld; Size = 53UL } :: c41Table.Entries }

    let c43Fields =
        [ methodGetFld
          { Name = ":scheme"; Values = [ "https" ] }
          { Name = ":path"; Values = [ "/index.html" ] }
          authorityFld
          customValueFld ]

    let c43Table =
        { c42Table with
            Entries = { Field = customValueFld; Size = 54UL } :: c42Table.Entries }

    [ TestCaseData("400a 6375 7374 6f6d 2d6b 6579 0d63 7573 746f 6d2d 6865 6164 6572", emptyTbl)
          .SetName("C.2.1. Literal Header Field with Indexing")
          .Returns(fieldsAndTable [ customHeaderFld ] [ { Field = customHeaderFld; Size = 55UL } ])

      TestCaseData("040c 2f73 616d 706c 652f 7061 7468", emptyTbl)
          .SetName("C.2.2. Literal Header Field without Indexing")
          .Returns(fieldsAndTable [ samplePathFld ] List.Empty)

      TestCaseData("1008 7061 7373 776f 7264 0673 6563 7265 74", emptyTbl)
          .SetName("C.2.3. Literal Header Field Never Indexed")
          .Returns(fieldsAndTable [ pwdFld ] List.Empty)

      TestCaseData("82", emptyTbl)
          .SetName("C.2.4. Indexed Header Field")
          .Returns(fieldsAndTable [ methodGetFld ] List.Empty)

      TestCaseData("8286 8441 8cf1 e3c2 e5f2 3a6b a0ab 90f4 ff", emptyTbl)
          .SetName("C.4.1. First Request")
          .Returns(c4Fields, c41Table)

      TestCaseData("8286 84be 5886 a8eb 1064 9cbf", c41Table)
          .SetName("C.4.2. Second Request")
          .Returns(c4Fields @ [ noCacheFld ], c42Table)

      TestCaseData("8287 85bf 4088 25a8 49e9 5ba9 7d7f 8925 a849 e95b b8e8 b4bf", c42Table)
          .SetName("C.4.3. Third Request")
          .Returns(c43Fields, c43Table)

      ]

[<TestCaseSource(nameof decodeFieldsTestCases)>]
let testDecodeBlock hex table =
    Table.decodeFields table (Hex.decodeSpan hex)
    |> Result.defaultValue([], Table.empty)
