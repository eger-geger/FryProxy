namespace FryProxy.Tests.Http

open System
open System.Net
open System.Net.Sockets
open System.Text

open System.Threading
open FryProxy.Http
open FryProxy.IO
open FryProxy.Extension
open FryProxy.IO.BufferedParser
open FryProxy.Tests.Constraints

open FsUnit
open NUnit.Framework

[<CancelAfter(5000)>]
type ParsersTests() =

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
        let memory = Array.zeroCreate<byte> 1024
        ReadBuffer(memory.AsMemory(), new NetworkStream(socket, true))

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

    let takeHeadN n (acc: int voption) : struct (int * string) voption Parser =
        let acc' = acc |> ValueOption.defaultValue 0 |> (+) 1

        if acc' > n then
            Parser.unit ValueNone
        else
            bufferedParser {
                let! line = Parse.utf8Line |> Parser.commit
                return ValueSome struct (acc', line)
            }

    let handleRequest (client: TcpClient) =
        let bytes =
            lines |> List.map addNewLine |> String.concat "" |> Encoding.UTF8.GetBytes

        task {
            use client = client
            use stream = client.GetStream()

            try
                do! stream.WriteAsync(bytes)
            with
            | :? SocketException as ex when ex.SocketErrorCode = SocketError.TimedOut -> ()
            | err -> TestContext.Error.WriteLine($"client error: {err}")
        }

    [<OneTimeSetUp>]
    member _.StartListener() =
        listener.Start()

        task {
            while listener.Server.IsBound do
                let! client = listener.AcceptTcpClientAsync()

                handleRequest client |> ignore
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

    [<Test>]
    member _.testUnfoldEmitsStateAndResult(ct: CancellationToken) =
        task {
            let buff = bufferedStream ()

            let! stream = Parser.unfold (takeHeadN 2) |> Parser.run buff
            let! results = stream.ToList(ct)

            results |> should equal
            <| [ struct (1, lines[0] + "\n"); struct (2, lines[1] + "\n") ]
        }

    [<Test>]
    member _.testAsyncEnumMap(ct: CancellationToken) =
        task {
            let buff = bufferedStream ()

            let! stream =
                Parser.unfold (takeHeadN 2)
                |> Parser.map (fun enum -> enum.Map(fun struct (s, r) -> $"%d{s}:%s{r.Trim()}"))
                |> Parser.run buff

            let! results = stream.ToList(ct)

            results |> should equal [ $"1:%s{lines[0]}"; $"2:%s{lines[1]}" ]
        }
