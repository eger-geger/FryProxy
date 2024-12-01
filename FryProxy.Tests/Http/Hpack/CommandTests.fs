module FryProxy.Tests.Http.Hpack.CommandTests

open NUnit.Framework
open FryProxy.Http.Hpack

let decodeTestCases =
    [ TestCaseData("82").SetName("Indexed Header Field").Returns(IndexedField 2u)

      TestCaseData("400a 6375 7374 6f6d 2d6b 6579 0d63 7573 746f 6d2d 6865 6164 6572")
          .SetName("Literal Header Field with Indexing")
          .Returns(IncIndexedField { Name = Literal "custom-key"; Value = "custom-header" })

      TestCaseData("040c 2f73 616d 706c 652f 7061 7468")
          .SetName("Literal Header Field without Indexing")
          .Returns(NonIndexedField { Name = Indexed 4u; Value = "/sample/path" })

      TestCaseData("1008 7061 7373 776f 7264 0673 6563 7265 74")
          .SetName("Literal Header Field Never Indexed")
          .Returns(NeverIndexedField { Name = Literal "password"; Value = "secret" }) ]

[<TestCaseSource(nameof decodeTestCases)>]
let testDecode (hex: string) =
    match Hex.decodeArr hex |> Command.decode 0 with
    | DecVal(cmd, _) -> cmd
    | DecErr(err, _) -> failwith err
