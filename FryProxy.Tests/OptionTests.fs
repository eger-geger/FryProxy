module FryProxy.Tests.OptionTests

open NUnit.Framework
open FsUnit

[<Test>]
let traverseEmptySeq () =
    Option.traverse Seq.empty |> should equal (Some List.empty)
    
[<Test>]
let traverseSeqWithSome () =
    Seq.map Some [1..5]
    |> Option.traverse
    |> should equal (Some [1..5])

[<Test>]
let traverseSeqWithNone() =
    [Some(1); None; Some(3)]
    |> Option.traverse
    |> should equal None