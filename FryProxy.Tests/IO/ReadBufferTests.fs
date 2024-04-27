module FryProxy.Tests.IO.ReadBufferTests

open System
open System.Buffers
open System.IO
open FryProxy.IO
open FsCheck.Experimental
open FsCheck.FSharp
open FsCheck.NUnit

type BufferedReader = MemoryStream * ReadBuffer

type BufferedReadModel =
    { input: byte[]
      buffer: byte[]
      bufferCapacity: int
      inputReads: byte[] list
      totalReads: byte[] list }


/// <summary>
/// Consume given number of bytes to buffer.
/// </summary>
let fillBuffer (n: int) (m: BufferedReadModel) =
    let read = m.input[.. n - 1]

    { m with
        input = m.input[n..]
        buffer = Array.append m.buffer read
        inputReads = read :: m.inputReads }


let equalityProp (desc: string) (act: 'a) (exp: 'a) =
    act = exp
    |> Prop.label (String.concat "\n\t" [ desc; $"act: %A{act}"; $"exp: %A{exp}" ])

/// Input stream unread bytes should match the model input.
let inputStreamMatchesModel (model: BufferedReadModel) (is: MemoryStream) =
    let inputArr = is.GetBuffer()[(int is.Position) ..]

    equalityProp "Input matches model" inputArr model.input
    |> Prop.trivial (Array.isEmpty model.input)

/// Buffer content should match model buffer
let bufferContentMatchesModel (model: BufferedReadModel) (buff: ReadBuffer) =
    let buffArr = buff.Pending.ToArray()

    equalityProp "Buffer matches model" buffArr model.buffer
    |> Prop.trivial (Array.isEmpty model.buffer)


type FillOp() =
    inherit Operation<BufferedReader, BufferedReadModel>()

    override _.Check(reader, model) =
        let is, buff = reader

        let byteCountMatchesModel =
            let expected = model.inputReads.Head.Length

            equalityProp "Number consumed bytes matches model"
            >> (|>) expected
            >> Prop.trivial (expected = 0)

        task {
            let! n = buff.Fill is

            return
                byteCountMatchesModel n
                .&. bufferContentMatchesModel model buff
                .&. inputStreamMatchesModel model is
        }
        |> Prop.ofTestable

    override _.Run m =
        match m.bufferCapacity - m.buffer.Length, m.input.Length with
        | 0, _ -> { m with inputReads = Array.empty :: m.inputReads }
        | _, 0 -> { m with inputReads = Array.empty :: m.inputReads }
        | b, l when b <= l -> fillBuffer b m
        | _, l -> fillBuffer l m

    override _.ToString() = "Fill"

/// Model reading given number of bytes
type ReadOp(n: int) =
    inherit Operation<BufferedReader, BufferedReadModel>()

    override _.Check(reader, model) =
        let is, buff = reader

        let readMatchesModel =
            equalityProp "Read bytes match model" >> (|>) model.totalReads.Head

        task {
            use memLock = MemoryPool.Shared.Rent(n)
            let mem = memLock.Memory.Slice(0, n)
            let! n = buff.Read is mem


            return
                readMatchesModel (mem.Slice(0, n).ToArray())
                .&. inputStreamMatchesModel model is
                .&. bufferContentMatchesModel model buff

        }
        |> Prop.ofTestable


    override _.Run m =
        if n = 0 then
            { m with totalReads = Array.empty :: m.totalReads }
        elif m.buffer.Length >= n then
            { m with
                buffer = m.buffer[n..]
                totalReads = m.buffer[.. n - 1] :: m.totalReads }
        else
            let b = n - m.buffer.Length

            let read, input' =
                if b <= m.input.Length then
                    m.input[.. b - 1], m.input[b..]
                else
                    m.input, Array.empty

            { m with
                input = input'
                buffer = Array.empty
                inputReads = read :: m.inputReads
                totalReads = Array.append m.buffer read :: m.totalReads }

    override _.ToString() = $"Read {n} bytes"

type PickSpanOp() =
    inherit FillOp()

    override _.Check(reader, model) =
        let is, buff = reader

        task {
            let! span = buff.PickSpan is

            return
                equalityProp "Buffer bytes match model" (span.ToArray()) model.buffer
                .&. bufferContentMatchesModel model buff
                .&. inputStreamMatchesModel model is
        }
        |> Prop.ofTestable

    override _.ToString() = "PickSpan"

type PickByteOp() =
    inherit FillOp()

    override _.Check(reader, model) =
        let is, buff = reader

        let byteMatchesModel =
            let expected = if Array.isEmpty model.buffer then None else Some model.buffer[0]
            equalityProp "Pick byte matches model" >> (|>) expected

        task {
            let! byte = buff.PickByte is

            return
                byteMatchesModel byte
                .&. bufferContentMatchesModel model buff
                .&. inputStreamMatchesModel model is
        }
        |> Prop.ofTestable

    override _.ToString() = "PickByte"


type CopyOp() =
    inherit Operation<BufferedReader, BufferedReadModel>()

    override _.Check(reader, model) =
        let is, buff = reader

        let copiedBytesMatchModel =
            equalityProp "Copied bytes match model" >> (|>) model.totalReads.Head

        let sourceStreamReadToCompletion =
            lazy (is.Position = is.Capacity)
            |> Prop.label "Source stream read to completion"

        task {
            use dst = new MemoryStream()

            do! buff.Copy is dst

            return
                sourceStreamReadToCompletion
                .&. copiedBytesMatchModel (dst.GetBuffer())
                .&. bufferContentMatchesModel model buff
                .&. inputStreamMatchesModel model is
        }
        |> Prop.ofTestable

    override _.Run m =
        { m with
            input = Array.empty
            buffer = Array.empty
            inputReads = m.input :: m.inputReads
            totalReads = Array.append m.buffer m.input :: m.totalReads }

    override this.ToString() = "Copy"


/// Model for discarding given number of bytes from buffer
type DiscardOp(n: int) =
    inherit Operation<BufferedReader, BufferedReadModel>()

    override _.Pre m = m.buffer.Length >= n

    override _.Check(reader, model) =
        let is, buff = reader
        buff.Discard(n)

        bufferContentMatchesModel model buff .&. inputStreamMatchesModel model is

    override _.Run m =
        { m with
            buffer = m.buffer[n..]
            inputReads = Array.empty :: m.inputReads
            totalReads = Array.empty :: m.totalReads }

    override _.ToString() = $"Discard({n})"

type ReaderSetup(bufferSize, source: byte array) =
    inherit Setup<BufferedReader, BufferedReadModel>()

    override _.Actual() =
        let is = new MemoryStream(source, 0, source.Length, false, true)
        let buffer = ReadBuffer(Memory(Array.zeroCreate bufferSize))
        is, buffer

    override _.Model() =
        { input = source
          buffer = Array.empty
          bufferCapacity = bufferSize
          inputReads = List.empty
          totalReads = List.empty }

type ReaderMachine() =
    inherit Machine<BufferedReader, BufferedReadModel>()

    override _.Next model =
        gen {
            let! readSize = seq { for n in 0..10 -> 2. ** n |> int } |> Gen.elements
            let! discardN = Gen.choose (0, model.buffer.Length)

            return!
                Gen.elements
                    [ FillOp()
                      ReadOp(readSize)
                      PickSpanOp()
                      PickSpanOp()
                      CopyOp()
                      DiscardOp(discardN) ]
        }

    override _.Setup =
        let sizes = seq { for n in 1..5 -> 2. ** n |> int }

        let setup =
            gen {
                let! bufferSize = Gen.elements sizes
                let! source = Gen.choose (0, 255) |> Gen.map byte |> Gen.arrayOf |> Gen.scaleSize ((*) 32)
                return ReaderSetup(bufferSize, source) :> Setup<BufferedReader, BufferedReadModel>
            }

        Arb.fromGen setup


[<Property(Parallelism = 4)>]
let testReadStreamBuffer () =
    let machine = ReaderMachine()
    StateMachine.toProperty machine
