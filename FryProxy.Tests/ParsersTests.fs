namespace FryProxy.Tests.IO

open System
open System.Buffers
open System.IO
open System.Net
open System.Net.Sockets
open System.Text
open FryProxy
open FryProxy.IO
open FryProxy.IO.BufferedParser
open NUnit.Framework
open FsUnit

type NetworkStreamWrapper(bytes: byte array) =

    let listener = new TcpListener(IPAddress.Loopback, 0)

    let listen () =
        listener.Start()

        task {
            use! socket = listener.AcceptSocketAsync()
            let! _ = socket.SendAsync(bytes)
            let! _ = socket.ReceiveAsync(bytes)
            ()
        }

    let connect () =
        task {
            let socket = new Socket(SocketType.Stream, ProtocolType.Tcp)
            do! socket.ConnectAsync(listener.LocalEndpoint)
            return new NetworkStream(socket, true)
        }

    interface IDisposable with
        member _.Dispose() =
            listener.Stop()
            listener.Dispose()

    member val Stream =
        task {
            do! listen ()
            return! connect ()
        }

[<Timeout(5000)>]
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

    let parseWordCount: BufferedParser.Parser<_, _> =
        Parse.utf8Line |> Parser.map wordCount

    let sentenceParser =
        bufferedParser {
            let! line = Parse.utf8Line

            if not (String.IsNullOrWhiteSpace line) then
                return line
            else
                return! Parser.failed
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
            let! one = bufferedStream () |> Parser.run (Parser.unit 1)

            one |> should equal (Some 1)
        }

    [<Test>]
    member _.testParseBuffer() =
        let buff = bufferedStream ()

        task {
            let! firstLine = Parser.run Parse.utf8Line buff
            let! secondLine = Parser.run Parse.utf8Line buff
            let! thirdLine = Parser.run Parse.utf8Line buff

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
            let! blankLine = Parser.run Parse.utf8Line state

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
