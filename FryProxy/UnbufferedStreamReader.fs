namespace FryProxy

open System.IO

type UnbufferedStreamReader(stream: Stream) =
    inherit TextReader()
    let mutable _lastPeeked: Option<int> = None

    override this.Peek(): int =
        match _lastPeeked with
        | None ->
            _lastPeeked <- this.Read() |> Some
            _lastPeeked.Value
        | Some value -> value

    override this.Read(): int =
        _lastPeeked <- None
        stream.ReadByte()
