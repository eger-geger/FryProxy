namespace FryProxy.Tests.IO

open System
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
          "Duis dapibus mauris at turpis ornare tristique."
          "     " ]

    let addNewLine s = s + "\n"

    let bufferedStream () =
        let bytes =
            lines |> List.map addNewLine |> String.concat "" |> Encoding.UTF8.GetBytes

        ReadBuffer(sharedMemory.Memory), new MemoryStream(bytes)

    let wordCount (str: string) = str.Trim().Split().Length

    let parseWordCount: int BufferedParser.Parser =
        utf8LineParser |> Parser.map wordCount

    let sentenceParser =
        bufferedParser {
            let! line = utf8LineParser

            if not (String.IsNullOrWhiteSpace line) then
                return line
        }

    [<Test>]
    member _.testUnit() =
        task {
            let! one = bufferedStream () |> Parser.run (Parser.unit 1)

            one |> should equal (Some 1)
        }

    [<Test>]
    member _.testParseBuffer() =
        let state = bufferedStream ()

        task {
            let buff = fst state

            let! firstLine = Parser.run utf8LineParser state
            let! secondLine = Parser.run utf8LineParser state
            let! thirdLine = Parser.run utf8LineParser state

            firstLine |> should equal (Some(lines.Head + "\n"))
            secondLine |> should equal (Some(lines[1] + "\n"))
            thirdLine |> should equal (Some(lines[2] + "\n"))

            buff.PendingSize |> should equal (lines[3].Length + 1)
        }

    [<Test>]
    member _.testEagerParser() =
        let state = bufferedStream ()

        task {
            let! sentences = Parser.run (Parser.eager sentenceParser) state
            let! blankLine = Parser.run utf8LineParser state

            sentences |> should equal (lines[..2] |> List.map addNewLine |> Some)
            blankLine |> should equal (lines[3] + "\n" |> Some)
        }

    [<Test>]
    member _.testMap() =
        task {
            let! wc = bufferedStream () |> Parser.run parseWordCount
            wc |> should equal (lines[0] |> wordCount |> Some)
        }


    [<Test>]
    member _.testParserFail() =
        let state = bufferedStream ()

        task {
            let! fMap = Parser.run (Parser.map ((+) 1) Parser.failed) state
            fMap |> should equal None
        }
