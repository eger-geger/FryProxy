namespace FryProxy.Tests.IO

open System
open System.Text
open FsUnit
open FryProxy.IO.ByteBuffer
open NUnit.Framework


type ByteBufferTests() =
    let buffer = Encoding.UTF8.GetBytes("hello" + "\n" + "world")

    let toByteArray (a: char array) = Array.map byte a

    [<Test>]
    member _.tryFindRangeTest() =
        tryFindRange [| 1uy |] buffer |> should equal None
        tryFindRange buffer buffer |> should equal (Some(0, buffer.Length - 1))
        tryFindRange ([| 'l'; 'l' |] |> toByteArray) buffer |> should equal (Some(2, 3))

        fun () -> tryFindRange Array.empty buffer |> ignore
        |> should throw typeof<ArgumentException>

    [<Test>]
    member _.tryTakeSuffixTest() =
        tryTakeSuffix [| 6uy |] buffer |> should equal None

        tryTakeSuffix ([| '\n' |] |> toByteArray) buffer
        |> should equal (Some([| 'h'; 'e'; 'l'; 'l'; 'o'; '\n' |] |> toByteArray))

    [<Test>]
    member _.tryTakeLineTest() =
        tryTakeUTF8Line buffer[..3] |> should equal None
        tryTakeUTF8Line buffer |> should equal (Some(6u, "hello\n"))
