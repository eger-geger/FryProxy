module FryProxy.Tests.Http.Hpack.CodecTests

open System
open FryProxy.Http.Hpack
open NUnit.Framework
open FsUnit

[<TestCase(3, 10UL, ExpectedResult = "1010")>]
[<TestCase(0, 42UL, ExpectedResult = "101010")>]
[<TestCase(3, 1337UL, ExpectedResult = "11111|10011010|1010")>]
let testEncodeNumber (offset, n) =
    NumericLit.from64 offset n
    |> NumericLit.toArray
    |> Array.map(sprintf "%B")
    |> Array.reduce(sprintf "%s|%s")

[<TestCase(3, "1010", ExpectedResult = 10UL)>]
[<TestCase(0, "101010", ExpectedResult = 42UL)>]
[<TestCase(3, "101010", ExpectedResult = 10UL)>]
[<TestCase(3, "11111|10011010|1010", ExpectedResult = 1337UL)>]
let testDecodeNumber (offset, bytes: string) =
    bytes.Split('|')
    |> Array.map(fun s -> Convert.ToByte(s, 2))
    |> NumericLit.decode offset 0
    |> Decoder.defaultValue NumericLit.zero
    |> NumericLit.to64

[<Test>]
let testDecodeNumberIncompleteSequence () =
    let bytes = [| 255uy; 255uy |]

    NumericLit.decode 0 0 bytes
    |> should be (ofCase <@ DecodeResult<NumericLit>.DecErr("", 0) @>)
