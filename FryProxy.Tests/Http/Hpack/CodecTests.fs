module FryProxy.Tests.Http.Hpack.CodecTests

open System
open FryProxy.Http.Hpack
open NUnit.Framework
open FsUnit

[<TestCase(3, 10UL, ExpectedResult = "1010")>]
[<TestCase(0, 42UL, ExpectedResult = "101010")>]
[<TestCase(3, 1337UL, ExpectedResult = "11111|10011010|1010")>]
let testEncodeNumber (offset, n) =
    (Codec.encodeInt offset n).ToArray()
    |> Array.map(sprintf "%B")
    |> Array.reduce(sprintf "%s|%s")

[<TestCase(3, "1010", ExpectedResult = 10UL)>]
[<TestCase(0, "101010", ExpectedResult = 42UL)>]
[<TestCase(3, "11111|10011010|1010", ExpectedResult = 1337UL)>]
let testDecodeNumber (offset, bytes: string) =
    bytes.Split('|')
    |> Array.map(fun s -> Convert.ToByte(s, 2))
    |> Codec.decodeInt offset

[<Test>]
let testDecodeNumberOverflow () =
    let bytes = Array.append <| Array.create 10 255uy <| [| 15uy |]

    (fun () -> Codec.decodeInt 0 bytes |> ignore)
    |> should throw typeof<OverflowException>

[<Test>]
let testDecodeNumberIncompleteSequence () =
    let bytes = [| 255uy; 255uy |]

    (fun () -> Codec.decodeInt 0 bytes |> ignore)
    |> should throw typeof<ArgumentException>
