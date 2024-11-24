module FryProxy.Tests.Http.Hpack.NumericTests

open System
open FryProxy.Http.Hpack
open NUnit.Framework
open FsUnit

[<TestCase(0, ExpectedResult = 255uy)>]
[<TestCase(1, ExpectedResult = 127uy)>]
[<TestCase(2, ExpectedResult = 63uy)>]
[<TestCase(3, ExpectedResult = 31uy)>]
[<TestCase(4, ExpectedResult = 15uy)>]
[<TestCase(5, ExpectedResult = 7uy)>]
[<TestCase(6, ExpectedResult = 3uy)>]
[<TestCase(7, ExpectedResult = 1uy)>]
[<TestCase(8, ExpectedResult = 0uy)>]
let testByteCap (offset: int) = NumericLit.byteCap offset

[<TestCase(3, 10UL, ExpectedResult = "1010")>]
[<TestCase(0, 42UL, ExpectedResult = "101010")>]
[<TestCase(3, 1337UL, ExpectedResult = "11111|10011010|1010")>]
let testEncode (offset, n) =
    NumericLit.from64 offset n
    |> NumericLit.toArray
    |> Array.map(sprintf "%B")
    |> Array.reduce(sprintf "%s|%s")

[<TestCase(3, "1010", ExpectedResult = 10us)>]
[<TestCase(0, "101010", ExpectedResult = 42us)>]
[<TestCase(3, "101010", ExpectedResult = 10us)>]
[<TestCase(3, "11111|10011010|1010", ExpectedResult = 1337us)>]
let testDecode (offset, bytes: string) =
    bytes.Split('|')
    |> Array.map(fun s -> Convert.ToByte(s, 2))
    |> NumericLit.decode offset 0
    |> Decoder.defaultValue NumericLit.zero
    |> NumericLit.uint16
    |> Result.defaultValue 0us


[<Test>]
let testOverflow () =
    3766us
    |> NumericLit.from16 4
    |> NumericLit.uint8
    |> should be (ofCase <@ Result<uint8, string>.Error("") @>)

[<Test>]
let testDecodeNumberIncompleteSequence () =
    let bytes = [| 255uy; 255uy |]

    NumericLit.decode 0 0 bytes
    |> should be (ofCase <@ DecodeResult<NumericLit>.DecErr("", 0) @>)
