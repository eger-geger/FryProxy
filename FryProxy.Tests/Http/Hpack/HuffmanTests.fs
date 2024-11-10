module FryProxy.Tests.Http.Hpack.HuffmanTests

open FryProxy.Http.Hpack
open NUnit.Framework
open FsUnit

let charSpace = [ 0..256 ] |> List.map char

[<TestCaseSource(nameof charSpace)>]
let ``code is defined for all ASCII symbols and EOS`` (char: char) =
    let code = Huffman.encodeChar char

    code.Char |> should equal char

    (code |> Huffman.msbCode |> Huffman.decodeChar) |> should equal code
