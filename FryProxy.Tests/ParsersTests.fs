namespace FryProxy.Tests.IO

open System
open System.Buffers
open System.IO
open System.Net
open System.Net.Sockets
open System.Text

open FsUnit
open NUnit.Framework

open FryProxy
open FryProxy.IO
open FryProxy.IO.BufferedParser
open FryProxy.Tests.Constraints

[<Timeout(5000); Parallelizable(ParallelScope.Fixtures)>]
type ParsersTests() =

    let sharedMemory = MemoryPool<byte>.Shared.Rent(1024)
    let listener = new TcpListener(IPAddress.Loopback, 0)

    let lines =
        [ "Lorem ipsum dolor sit amet, consectetur adipiscing elit."
          "Donec fringilla nisl vitae mauris posuere, eu lacinia elit tempor."
          "Duis dapibus mauris at turpis ornare tristique."
          "     " ]

    let addNewLine s = s + "\n"

    let bufferedStream () =
        let socket = new Socket(SocketType.Stream, ProtocolType.Tcp)
        socket.Connect(listener.LocalEndpoint)
        ReadBuffer(sharedMemory.Memory, new NetworkStream(socket, true))

    let wordCount (str: string) = str.Trim().Split().Length

    let parseWordCount: BufferedParser.Parser<_> =
        Parse.utf8Line |> Parser.map wordCount

    let sentenceParser =
        bufferedParser {
            let! line = Parse.utf8Line

            if not (String.IsNullOrWhiteSpace line) then
                return line
            else
                return! Parser.failed "Empty string"
        }

    [<OneTimeSetUp>]
    member _.StartListener() =
        let bytes =
            lines |> List.map addNewLine |> String.concat "" |> Encoding.UTF8.GetBytes

        listener.Start()

        task {
            while listener.Server.IsBound do
                let! client = listener.AcceptTcpClientAsync()
                client.ReceiveTimeout <- 2000

                do! client.GetStream().WriteAsync(bytes)

                try
                    client.GetStream().ReadByte() |> ignore
                with :? IOException ->
                    ()
        }
        |> ignore

    [<OneTimeTearDown>]
    member _.StopListener() = listener.Dispose()

    [<Test>]
    member _.testUnit() =
        task {
            let! one = (Parser.unit 1) |> Parser.run (bufferedStream ())

            one |> should equal 1
        }

    [<Test>]
    member _.testParseBuffer() =
        let buff = bufferedStream ()

        task {
            let! firstLine = Parser.run buff Parse.utf8Line
            let! secondLine = Parser.run buff Parse.utf8Line
            let! thirdLine = Parser.run buff Parse.utf8Line

            firstLine |> should equal (lines.Head + "\n")
            secondLine |> should equal (lines[1] + "\n")
            thirdLine |> should equal (lines[2] + "\n")

            buff.PendingSize |> should equal (lines[3].Length + 1)
        }

    [<Test>]
    member _.testEagerParser() =
        let buff = bufferedStream ()

        task {
            let! sentences = Parser.run buff (Parser.eager sentenceParser)
            let! blankLine = Parser.run buff Parse.utf8Line

            sentences |> should equal (lines[..2] |> List.map addNewLine)
            blankLine |> should equal (lines[3] + "\n")
        }

    [<Test>]
    member _.testMap() =
        task {
            let! wc = parseWordCount |> Parser.run (bufferedStream ())
            wc |> should equal (lines[0] |> wordCount)
        }


    [<Test>]
    member _.testParserFail() =
        let rb = bufferedStream ()

        Parser.failed "Fail"
        |> Parser.map ((+) 1)
        |> Parser.run rb
        |> shouldThrowAsync<ParseError>.From
