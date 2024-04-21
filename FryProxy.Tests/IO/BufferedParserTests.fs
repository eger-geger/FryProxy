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

    [<Test>]
    member _.parseBufferTest() =
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
    member _.eagerParserTest() =
        let state = bufferedStream ()

        task {
            let! parsedLines = BufferedParser.eager BufferedParser.parseUTF8Line state
            let! emptyParser = BufferedParser.eager BufferedParser.parseUTF8Line state
            
            parsedLines |> should equal (Some(List.map addNewLine lines[..1]))
            emptyParser |> should equal None
        }
