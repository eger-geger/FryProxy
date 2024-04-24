namespace FryProxy.Tests.IO

open System.Buffers
open System.IO
open System.Text
open FryProxy.IO
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

        ReadStreamBuffer(sharedMemory.Memory), new MemoryStream(bytes)

    let addNewLine s = s + "\n"

    let wordCount (str: string) = str.Trim().Split().Length

    let parseWordCount: int BufferedParser.Parser =
        BufferedParser.parseUTF8Line |> BufferedParser.map wordCount

    [<Test>]
    member _.testUnit() =
        task {
            let! one = BufferedParser.unit 1 <| bufferedStream ()

            one |> should equal (Some 1)
        }

    [<Test>]
    member _.testParseBuffer() =
        let state = bufferedStream ()

        task {
            let buff = fst state

            let! firstLine = BufferedParser.parseUTF8Line state
            let! secondLine = BufferedParser.parseUTF8Line state
            let! thirdLine = BufferedParser.parseUTF8Line state

            firstLine |> should equal (Some(lines.Head + "\n"))
            secondLine |> should equal (Some(lines[1] + "\n"))
            thirdLine |> should equal None

            buff.PendingSize |> should equal lines[2].Length
        }

    [<Test>]
    member _.testEagerParser() =
        let state = bufferedStream ()

        task {
            let! parsedLines = BufferedParser.eager BufferedParser.parseUTF8Line state
            let! emptyParser = BufferedParser.eager BufferedParser.parseUTF8Line state

            parsedLines |> should equal (lines[..1] |> List.map addNewLine |> Some)
            emptyParser |> should equal None
        }

    [<Test>]
    member _.testMap() =
        task {
            let! wc = parseWordCount <| bufferedStream ()
            wc |> should equal (lines[0] |> wordCount |> Some)
        }

    [<Test>]
    member _.testMap2() =
        let parseWordCount2 = BufferedParser.map2 (+) parseWordCount parseWordCount

        task {
            let! wc2 = parseWordCount2 <| bufferedStream ()

            wc2 |> should equal (lines[..1] |> List.map wordCount |> List.sum |> Some)
        }

    [<Test>]
    member _.testFlatOpt() =
        task {
            let! wc =
                parseWordCount |> BufferedParser.map Some |> BufferedParser.flatOpt
                <| bufferedStream ()

            wc |> should equal (lines[0] |> wordCount |> Some)
        }

    [<Test>]
    member _.testOrElse() =
        task {
            let! one =
                BufferedParser.unit None
                |> BufferedParser.flatOpt
                |> BufferedParser.orElse (BufferedParser.unit 1)
                <| bufferedStream ()

            one |> should equal (Some 1)
        }

    [<Test>]
    member _.testParserFail() =
        let state = bufferedStream ()
        let one = BufferedParser.unit 1
        let failed = BufferedParser.parseBuffer (fun _ -> None)

        task {
            let! fEager = BufferedParser.eager failed <| state
            fEager |> should equal None

            let! fMap = BufferedParser.map ((+) 1) failed <| state
            fMap |> should equal None

            let! fMap21 = BufferedParser.map2 (+) one failed <| state
            fMap21 |> should equal None

            let! fMap22 = BufferedParser.map2 (+) failed one <| state
            fMap22 |> should equal None

            let! fMap23 = BufferedParser.map2 (+) failed failed <| state
            fMap23 |> should equal None

            let! fFlatOpt1 = failed |> BufferedParser.map Some |> BufferedParser.flatOpt <| state
            fFlatOpt1 |> should equal None

            let! fFlatOpt2 = one |> BufferedParser.map (fun _ -> None) |> BufferedParser.flatOpt <| state
            fFlatOpt2 |> should equal None

            let! fOrElse = BufferedParser.orElse failed failed <| state
            fOrElse |> should equal None
        }
