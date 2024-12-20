module FryProxy.Tests.Http.Hpack.CommandTests

open NUnit.Framework
open FryProxy.Http.Hpack

let decodeCommandTestCases =
    [ TestCaseData("82")
          .SetName("C.2.4 Indexed Header Field")
          .Returns(IndexedField 2us)

      TestCaseData("400a 6375 7374 6f6d 2d6b 6579 0d63 7573 746f 6d2d 6865 6164 6572")
          .SetName("C.2.1 Literal Header Field with Indexing")
          .Returns(IndexedLiteralField { Name = Literal "custom-key"; Value = "custom-header" })

      TestCaseData("040c 2f73 616d 706c 652f 7061 7468")
          .SetName("C.2.2 Literal Header Field without Indexing")
          .Returns(NonIndexedLiteralField { Name = Indexed 4us; Value = "/sample/path" })

      TestCaseData("1008 7061 7373 776f 7264 0673 6563 7265 74")
          .SetName("C.2.3 Literal Header Field Never Indexed")
          .Returns(NeverIndexedLiteralField { Name = Literal "password"; Value = "secret" }) ]

[<TestCaseSource(nameof decodeCommandTestCases)>]
let testDecodeCommand (hex: string) =
    match Command.decodeCommand.Invoke(Hex.decodeArr hex) with
    | Ok cmd, _ -> cmd
    | Error err, _ -> failwith err

let encodeCommandTestCases =
    decodeCommandTestCases
    |> List.map(fun tc -> TestCaseData(tc.ExpectedResult).Returns(tc.Arguments[0]).SetName(tc.TestName))

[<TestCaseSource(nameof encodeCommandTestCases)>]
let testEncodeCommand (cmd: Command) =
    let octets = Command.encodeCommand cmd
    Hex.encodeSeq(octets.ToArray())

let blockTestCases =
    let firstRequest =
        [ IndexedField(2us)
          IndexedField(6us)
          IndexedField(4us)
          IndexedLiteralField({ Name = Indexed(1us); Value = "www.example.com" }) ]

    let secondRequest =
        [ IndexedField(2us)
          IndexedField(6us)
          IndexedField(4us)
          IndexedField(62us)
          IndexedLiteralField({ Name = Indexed(24us); Value = "no-cache" }) ]

    let thirdRequest =
        [ IndexedField(2us)
          IndexedField(7us)
          IndexedField(5us)
          IndexedField(63us)
          IndexedLiteralField({ Name = Literal("custom-key"); Value = "custom-value" }) ]

    [ TestCaseData("", TestName = "Empty", ExpectedResult = List.empty<Command>)

      TestCaseData("8286 8441 0f77 7777 2e65 7861 6d70 6c65 2e63 6f6d")
          .SetName("C.3.1 First Request")
          .Returns(firstRequest)

      TestCaseData("8286 8441 8cf1 e3c2 e5f2 3a6b a0ab 90f4 ff")
          .SetName("C.4.1 First Request")
          .SetCategory("Huffman")
          .Returns(firstRequest)

      TestCaseData("8286 84be 5808 6e6f 2d63 6163 6865")
          .SetName("C.3.2 Second Request")
          .Returns(secondRequest)

      TestCaseData("8286 84be 5886 a8eb 1064 9cbf")
          .SetName("C.4.2 Second Request")
          .SetCategory("Huffman")
          .Returns(secondRequest)

      TestCaseData("8287 85bf 400a 6375 7374 6f6d 2d6b 6579 0c63 7573 746f 6d2d 7661 6c75 65")
          .SetName("C.3.3 Third Request")
          .Returns(thirdRequest)

      TestCaseData("8287 85bf 4088 25a8 49e9 5ba9 7d7f 8925 a849 e95b b8e8 b4bf")
          .SetName("C.4.3 Third Request")
          .SetCategory("Huffman")
          .Returns(thirdRequest) ]

[<TestCaseSource(nameof blockTestCases)>]
let testDecodeBlock (hex: string) =
    match Hex.decodeArr hex |> Decoder.runArr Command.decodeBlock with
    | Ok commands -> commands
    | Error err -> failwith err
