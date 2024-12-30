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
let testByteCap (offset: int) = NumericLit.octetCap offset

[<TestCase(3, 10UL, ExpectedResult = "0a")>]
[<TestCase(0, 42UL, ExpectedResult = "2a")>]
[<TestCase(3, 1337UL, ExpectedResult = "1f9a 0a")>]
let testEncode (prefix, n) =
    Hex.OctetWriter(NumericLit.encode prefix n) |> Hex.runWriter

[<TestCase(3, "1010", ExpectedResult = 10us)>]
[<TestCase(0, "101010", ExpectedResult = 42us)>]
[<TestCase(3, "101010", ExpectedResult = 10us)>]
[<TestCase(3, "11111|10011010|1010", ExpectedResult = 1337us)>]
let testDecode (offset, bytes: string) =
    bytes.Split('|')
    |> Array.map(fun s -> Convert.ToByte(s, 2))
    |> Decoder.runArr(NumericLit.decode offset)
    |> Result.defaultValue NumericLit.zero
    |> NumericLit.toUint16
    |> Result.defaultValue 0us


[<Test>]
let testValueOverflow () =
    3766u
    |> NumericLit.create
    |> NumericLit.toUint8
    |> should be (ofCase <@ Result<uint8, string>.Error("") @>)

[<TestCase("ffff")>]
[<TestCase("fffe ffff ff0f")>]
let testDecodeError (hex: string) =
    let octets = Hex.decodeSpan hex
    
    let struct (res, _) = (NumericLit.decode 0).Invoke octets
    res |> should be (ofCase <@ Result<NumericLit, string>.Error("") @>)
