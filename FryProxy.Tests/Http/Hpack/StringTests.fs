module FryProxy.Tests.Http.Hpack.StringTests

open System
open FryProxy.Http.Hpack
open NUnit.Framework

[<Category("Raw")>]
[<TestCase(8us, "6e6f 2d63 6163 6865", ExpectedResult = "no-cache")>]
[<TestCase(10us, "6375 7374 6f6d 2d6b 6579", ExpectedResult = "custom-key")>]
[<TestCase(12us, "6375 7374 6f6d 2d76 616c 7565", ExpectedResult = "custom-value")>]
[<TestCase(15us, "7777 772e 6578 616d 706c 652e 636f 6d", ExpectedResult = "www.example.com")>]
let testDecodeRaw (len: uint16, hex: string) =
    Hex.decodeArr hex
    |> Decoder.runArr(StringLit.decodeRaw len)
    |> Result.defaultValue String.Empty


[<Category("Raw")>]
[<TestCase("no-cache", ExpectedResult = "086e 6f2d 6361 6368 65")>]
[<TestCase("custom-key", ExpectedResult = "0a63 7573 746f 6d2d 6b65 79")>]
[<TestCase("custom-value", ExpectedResult = "0c63 7573 746f 6d2d 7661 6c75 65")>]
[<TestCase("www.example.com", ExpectedResult = "0f77 7777 2e65 7861 6d70 6c65 2e63 6f6d")>]
let testEncodeRaw (str: string) =
    Hex.OctetWriter(StringLit.encodeRaw str) |> Hex.runWriter

[<Category("Huffman")>]
[<TestCase(6us, "a8eb 1064 9cbf", ExpectedResult = "no-cache")>]
[<TestCase(8us, "25a8 49e9 5ba9 7d7f", ExpectedResult = "custom-key")>]
[<TestCase(9us, "25a8 49e9 5bb8 e8b4 bf", ExpectedResult = "custom-value")>]
[<TestCase(12us, "f1e3 c2e5 f23a 6ba0 ab90 f4ff", ExpectedResult = "www.example.com")>]
let testDecodeHuf (len: uint16, hex: string) =
    Hex.decodeArr hex
    |> Decoder.runArr(StringLit.decodeHuf len)
    |> Result.defaultValue String.Empty

[<Category("Huffman")>]
[<TestCase("no-cache", ExpectedResult = "86a8 eb10 649c bf")>]
[<TestCase("custom-key", ExpectedResult = "8825 a849 e95b a97d 7f")>]
[<TestCase("custom-value", ExpectedResult = "8925 a849 e95b b8e8 b4bf")>]
[<TestCase("www.example.com", ExpectedResult = "8cf1 e3c2 e5f2 3a6b a0ab 90f4 ff")>]
let testEncodeHuf (str: string) =
    Hex.OctetWriter(StringLit.encodeHuf str) |> Hex.runWriter

[<TestCase("86 a8eb 1064 9cbf", ExpectedResult = "no-cache", Category = "Huffman")>]
[<TestCase("08 6e6f 2d63 6163 6865", ExpectedResult = "no-cache", Category = "Raw")>]
[<TestCase("88 25a8 49e9 5ba9 7d7f", ExpectedResult = "custom-key", Category = "Huffman")>]
[<TestCase("0a 6375 7374 6f6d 2d6b 6579", ExpectedResult = "custom-key", Category = "Raw")>]
[<TestCase("89 25a8 49e9 5bb8 e8b4 bf", ExpectedResult = "custom-value", Category = "Huffman")>]
[<TestCase("0c 6375 7374 6f6d 2d76 616c 7565", ExpectedResult = "custom-value", Category = "Raw")>]
[<TestCase("8c f1e3 c2e5 f23a 6ba0 ab90 f4ff", ExpectedResult = "www.example.com", Category = "Huffman")>]
[<TestCase("0f 7777 772e 6578 616d 706c 652e 636f 6d", ExpectedResult = "www.example.com", Category = "Raw")>]
let testDecode (hex: string) =
    Hex.decodeArr hex
    |> Decoder.runArr StringLit.decode
    |> Result.defaultValue String.Empty
