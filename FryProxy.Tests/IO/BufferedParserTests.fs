namespace FryProxy.Tests.IO

open System.Buffers
open System.IO
open System.Text
open FryProxy.IO
open FryProxy.IO.BufferedParser
open NUnit.Framework
open FsUnit

type BufferedParserTests() =

    let sharedMemory = MemoryPool<byte>.Shared.Rent(1024)

    let lines =
        [ "Lorem ipsum dolor sit amet, consectetur adipiscing elit."
          "Donec fringilla nisl vitae mauris posuere, eu lacinia elit tempor."
          "Duis dapibus mauris at turpis ornare tristique." ]

    let bufferedStream () =
        let bytes = String.concat "\n" lines |> Encoding.UTF8.GetBytes

        ReadBuffer(sharedMemory.Memory), new MemoryStream(bytes)

    let addNewLine s = s + "\n"

    let wordCount (str: string) = str.Trim().Split().Length

    let parseWordCount: int BufferedParser.Parser =
        Parser.parseUTF8Line |> Parser.map wordCount

    [<Test>]
    member _.testUnit() =
        task {
            let! one = Parser.unit 1 <| bufferedStream ()

            one |> should equal (Some 1)
        }

    [<Test>]
    member _.testParseBuffer() =
        let state = bufferedStream ()

        task {
            let buff = fst state

            let! firstLine = Parser.parseUTF8Line state
            let! secondLine = Parser.parseUTF8Line state
            let! thirdLine = Parser.parseUTF8Line state

            firstLine |> should equal (Some(lines.Head + "\n"))
            secondLine |> should equal (Some(lines[1] + "\n"))
            thirdLine |> should equal None

            buff.PendingSize |> should equal lines[2].Length
        }

    [<Test>]
    member _.testEagerParser() =
        let state = bufferedStream ()

        task {
            let! parsedLines = Parser.eager Parser.parseUTF8Line state
            let! emptyResult = Parser.eager Parser.parseUTF8Line state

            parsedLines |> should equal (lines[..1] |> List.map addNewLine |> Some)
            emptyResult = Some(List.empty) |> should equal true
        }

    [<Test>]
    member _.testMap() =
        task {
            let! wc = parseWordCount <| bufferedStream ()
            wc |> should equal (lines[0] |> wordCount |> Some)
        }


    [<Test>]
    member _.testParserFail() =
        let state = bufferedStream ()

        task {
            let! fMap = Parser.map ((+) 1) Parser.failed <| state
            fMap |> should equal None
        }
