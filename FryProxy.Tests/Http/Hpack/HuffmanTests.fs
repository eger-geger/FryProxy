module FryProxy.Tests.Http.Hpack.HuffmanTests

open FryProxy.Http.Hpack
open NUnit.Framework
open FsUnit

let charSpace = [ 0..256 ] |> List.map char

[<TestCaseSource(nameof charSpace)>]
let ``code is defined for all ASCII symbols and EOS`` (char: char) =
    let code = Huffman.encodeChar char

    code.Char |> should equal char

    (code.Code |> Huffman.alignLeft |> _.Value |> Huffman.decodeChar) |> should equal code.Char

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


[<TestCase(0b010110u, 6uy, ExpectedResult = 0b010110u)>]
[<TestCase(0b11111110_10u, 10uy, ExpectedResult = 0b10_11111110u)>]
[<TestCase(0b11111111_1111100u, 15uy, ExpectedResult = 0b1111100_11111111u)>]
[<TestCase(0b11111111_11111110_1100u, 20uy, ExpectedResult = 0b1100_11111110_11111111u)>]
let testLittleEndian (path: uint32, bitLen) =
    { Huffman.Value = path; Huffman.Size = bitLen }
    |> Huffman.littleEndian
    |> _.Value
