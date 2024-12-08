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

[<Category("Huffman")>]
[<TestCase(6us, "a8eb 1064 9cbf", ExpectedResult = "no-cache")>]
[<TestCase(8us, "25a8 49e9 5ba9 7d7f", ExpectedResult = "custom-key")>]
[<TestCase(9us, "25a8 49e9 5bb8 e8b4 bf", ExpectedResult = "custom-value")>]
[<TestCase(12us, "f1e3 c2e5 f23a 6ba0 ab90 f4ff", ExpectedResult = "www.example.com")>]
let testDecodeHuf (len: uint16, hex: string) =
    Hex.decodeArr hex
    |> Decoder.runArr(StringLit.decodeHuf len)
    |> Result.defaultValue String.Empty

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
