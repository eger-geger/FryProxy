module FryProxy.Tests.Http.Hpack.TableTests

open FryProxy.Http
open FryProxy.Http.Hpack

open NUnit.Framework

[<Literal>]
let TableSizeLimit = 0xffffUL

let fieldsAndTable fields entries =
    fields, { Table.Entries = entries; Table.SizeLimit = TableSizeLimit }

let decodeFieldsTestCases =
    let customFld = { Name = "custom-key"; Values = [ "custom-header" ] }
    let pathFld = { Name = ":path:"; Values = [ "/sample/path" ] }
    let pwdFld = { Name = "password"; Values = [ "secret" ] }
    let getFld = { Name = ":method:"; Values = [ "GET" ] }

    [ TestCaseData("400a 6375 7374 6f6d 2d6b 6579 0d63 7573 746f 6d2d 6865 6164 6572")
          .SetName("C.2.1. Literal Header Field with Indexing")
          .Returns(fieldsAndTable [ customFld ] [ { Field = customFld; Size = 55UL } ])

      TestCaseData("040c 2f73 616d 706c 652f 7061 7468")
          .SetName("C.2.2. Literal Header Field without Indexing")
          .Returns(fieldsAndTable [ pathFld ] List.Empty)

      TestCaseData("1008 7061 7373 776f 7264 0673 6563 7265 74")
          .SetName("C.2.3. Literal Header Field Never Indexed")
          .Returns(fieldsAndTable [ pwdFld ] List.Empty)

      TestCaseData("82")
          .SetName("C.2.4. Indexed Header Field")
          .Returns(fieldsAndTable [ getFld ] List.Empty)

      ]

[<TestCaseSource(nameof decodeFieldsTestCases)>]
let testDecodeBlock (hex: string) =
    let emptyTable = { Table.empty with SizeLimit = TableSizeLimit }

    Table.decodeFields emptyTable (Hex.decodeSpan hex)
    |> Result.defaultValue([], Table.empty)
