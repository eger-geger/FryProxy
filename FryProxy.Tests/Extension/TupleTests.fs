module FryProxy.Tests.TupleTests

open System
open NUnit.Framework
open FsUnit

[<Test>]
let append1Test () = Tuple.append1 22 11 |> should equal (11, 22)

[<Test>]
let append2Test() = Tuple.append2 44 (11, 22) |> should equal (11, 22, 44)

[<Test>]
let create2Test() = Tuple.create2 11 22 |> should equal (11, 22)

[<Test>]
let map2of3Test() = Tuple.map2of3 ((*) 2) (11, 22, 33) |> should equal (11, 44, 33)