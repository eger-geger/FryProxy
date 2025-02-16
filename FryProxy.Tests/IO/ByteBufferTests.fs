module FryProxy.Tests.IO.ByteBufferTests

#nowarn "3391"

open FryProxy.IO.ByteBuffer

open System
open System.Text

open FsUnit
open NUnit.Framework

let buffer = Encoding.UTF8.GetBytes("hello" + "\n" + "world")

[<Test>]
let tryFindSliceTest () =
    let noneSlice: int voption = ValueNone

    tryFindSlice buffer buffer 0 |> should equal (ValueSome 0)
    tryFindSlice ReadOnlySpan.Empty buffer 0 |> should equal (ValueSome 0)
    tryFindSlice (ReadOnlySpan [| 1uy |]) buffer 0 |> should equal noneSlice

    tryFindSlice (ReadOnlySpan [| byte 'l'; byte 'l' |]) buffer 0
    |> should equal (ValueSome 2)

[<Test>]
let tryTakePrefixTest () =
    let nonePrefix: byte ReadOnlyMemory voption = ValueNone

    tryTakePrefix (ReadOnlySpan [| 6uy |]) buffer |> should equal nonePrefix

    fun () -> tryTakePrefix ReadOnlySpan.Empty buffer |> ignore
    |> should throw typeof<ArgumentException>

    tryTakePrefix (ReadOnlySpan [| byte '\n' |]) buffer
    |> ValueOption.map(fun prefix -> prefix.ToArray() |> Array.map char |> String)
    |> should equal (ValueSome "hello\n")

[<Test>]
let tryTakeLineTest () =
    let noneLine: struct (uint16 * string) voption = ValueNone

    tryTakeUTF8Line(buffer[..3]) |> should equal noneLine
    tryTakeUTF8Line buffer |> should equal (ValueSome struct (6us, "hello\n"))
