module FryProxy.Tests.Http.Hpack.StringTests

open System
open FryProxy.Http.Hpack
open NUnit.Framework
open FsUnit

let decodeHexArr (hex: string) =
    let rec loop (s: string) acc =
        if String.IsNullOrEmpty s then
            acc |> List.rev
        else
            Convert.ToByte(s[0..1], 16) :: acc |> loop s[2..]

    loop (hex.Replace(" ", "")) List.empty |> List.toArray

[<TestCase(8us, "6e6f 2d63 6163 6865", ExpectedResult = "no-cache")>]
[<TestCase(10us, "6375 7374 6f6d 2d6b 6579", ExpectedResult = "custom-key")>]
[<TestCase(12us, "6375 7374 6f6d 2d76 616c 7565", ExpectedResult = "custom-value")>]
[<TestCase(15us, "7777 772e 6578 616d 706c 652e 636f 6d", ExpectedResult = "www.example.com")>]
let testDecodeRaw (len: uint16, hex: string) =
    decodeHexArr hex
    |> StringLit.decodeRaw len 0
    |> Decoder.defaultValue String.Empty

[<TestCase("08 6e6f 2d63 6163 6865", ExpectedResult = "no-cache")>]
[<TestCase("0a 6375 7374 6f6d 2d6b 6579", ExpectedResult = "custom-key")>]
[<TestCase("0c 6375 7374 6f6d 2d76 616c 7565", ExpectedResult = "custom-value")>]
[<TestCase("0f 7777 772e 6578 616d 706c 652e 636f 6d", ExpectedResult = "www.example.com")>]
let testDecode (hex: string) =
    decodeHexArr hex |> StringLit.decode 0 |> Decoder.defaultValue String.Empty
