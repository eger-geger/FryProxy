module FryProxy.Tests.IO.ByteBufferTests

open FryProxy.IO.ByteBuffer

open System
open System.Text

open FsUnit
open NUnit.Framework

let buffer = ReadOnlyMemory(Encoding.UTF8.GetBytes("hello" + "\n" + "world"))

[<Test>]
let tryFindRangeTest () =
    tryFindSlice [| 1uy |] buffer |> should equal None
    tryFindSlice (buffer.ToArray()) buffer |> should equal (Some 0)
    tryFindSlice [| byte 'l'; byte 'l' |] buffer |> should equal (Some 2)

    fun () -> tryFindSlice Array.empty buffer |> ignore
    |> should throw typeof<ArgumentException>

[<Test>]
let tryTakeSuffixTest () =
    tryTakeSuffix [| 6uy |] buffer |> should equal None

    tryTakeSuffix [| byte '\n' |] buffer
    |> Option.map(fun mem -> mem.ToArray() |> Array.map char |> String)
    |> should equal (Some "hello\n")

[<Test>]
let tryTakeLineTest () =
    tryTakeUTF8Line(buffer.Slice(0, 3)) |> should equal None
    tryTakeUTF8Line buffer |> should equal (Some struct (6us, "hello\n"))
