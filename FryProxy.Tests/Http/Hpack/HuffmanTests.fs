module FryProxy.Tests.Http.Hpack.HuffmanTests

open FryProxy.Http.Hpack
open NUnit.Framework
open FsUnit

let charSpace = [ 0..256 ] |> List.map char

[<TestCaseSource(nameof charSpace)>]
let ``code is defined for all ASCII symbols and EOS`` (char: char) =
    let code = Huffman.encodeChar char

    code.Char |> should equal char

    (code.Code |> Huffman.alignLeft |> _.Value |> Huffman.decodeChar)
    |> should equal code.Char

[<TestCase(0b11111110uy)>]
[<TestCase(0b11111100uy)>]
[<TestCase(0b11111000uy)>]
[<TestCase(0b11110000uy)>]
[<TestCase(0b11100000uy)>]
[<TestCase(0b11000000uy)>]
[<TestCase(0b10000000uy)>]
let testValidPadding (pad: uint8) =
    Huffman.checkPadding (uint32 pad <<< 24) ()
    |> should equal (Result<unit, string>.Ok())

[<TestCase(0b11111111uy)>]
[<TestCase(0b11000001uy)>]
[<TestCase(0b00000000uy)>]
let testInvalidPadding (pad: uint8) =
    Huffman.checkPadding (uint32 pad <<< 24) ()
    |> should be (ofCase <@ Result<unit, string>.Error @>)


[<TestCase("no-cache", ExpectedResult = "a8eb 1064 9cbf")>]
[<TestCase("custom-key", ExpectedResult = "25a8 49e9 5ba9 7d7f")>]
[<TestCase("custom-value", ExpectedResult = "25a8 49e9 5bb8 e8b4 bf")>]
[<TestCase("www.example.com", ExpectedResult = "f1e3 c2e5 f23a 6ba0 ab90 f4ff")>]
let testEncodeString (str: string) : string =
    Hex.OctetWriter(Huffman.encodeStr str) |> Hex.runWriter

[<TestCase("a8eb 1064 9cbf", ExpectedResult = "no-cache")>]
[<TestCase("25a8 49e9 5ba9 7d7f", ExpectedResult = "custom-key")>]
[<TestCase("25a8 49e9 5bb8 e8b4 bf", ExpectedResult = "custom-value")>]
[<TestCase("f1e3 c2e5 f23a 6ba0 ab90 f4ff", ExpectedResult = "www.example.com")>]
let testDecodeString (hex: string) : string =
    hex |> Hex.decodeArr |> Huffman.decodeStr |> Result.defaultValue ""
