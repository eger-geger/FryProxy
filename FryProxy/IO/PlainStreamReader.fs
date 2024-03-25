namespace FryProxy

open System.IO

type PlainStreamReader(stream: Stream) =
    inherit TextReader()
    let mutable _nextByte: Option<int> = None

    override this.Peek() : int =
        _nextByte <- this.Read() |> Some
        _nextByte.Value

    override this.Read() : int =
        try
            Option.defaultWith stream.ReadByte _nextByte
        finally
            _nextByte <- None
