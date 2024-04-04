module FryProxy.Tests.IO.ReadStreamBufferTests

open System
open System.Buffers
open System.IO
open FryProxy.IO.ReadStreamBuffer
open FsCheck.Experimental
open FsCheck.FSharp
open FsCheck.NUnit

type BufferedReader = MemoryStream * ReadStreamBuffer

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

let inputStreamMatchesModel (model: BufferedReadModel) (is: MemoryStream) =
    let inputArr = is.GetBuffer()[(int is.Position) ..]

    equalityProp "Input matches model" inputArr model.input
    |> Prop.trivial (Array.isEmpty model.input)

let bufferContentMatchesModel (model: BufferedReadModel) (buff: ReadStreamBuffer) =
    let bufArr = buff.Pending.ToArray()

    equalityProp "Buffer matches model" bufArr model.buffer
    |> Prop.trivial (Array.isEmpty model.buffer)


type FillOp() =
    inherit Operation<BufferedReader, BufferedReadModel>()

    override _.Check(reader, model) =
        let is, buf = reader

        let byteCountMatchesModel =
            let expected = model.inputReads.Head.Length

            equalityProp "Number consumed bytes matches model"
            >> (|>) expected
            >> Prop.trivial (expected = 0)

        task {
            let! n = buf.fill is

            return
                byteCountMatchesModel n
                .&. bufferContentMatchesModel model buf
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

type ReadOp(byteCount: int) =
    inherit Operation<BufferedReader, BufferedReadModel>()

    override _.Check(reader, model) =
        let is, buf = reader

        let readMatchesModel =
            equalityProp "Read bytes match model" >> (|>) model.totalReads.Head

        task {
            use memLock = MemoryPool.Shared.Rent(byteCount)
            let mem = memLock.Memory.Slice(0, byteCount)
            let! n = buf.read is mem


            return
                readMatchesModel (mem.Slice(0, n).ToArray())
                .&. inputStreamMatchesModel model is
                .&. bufferContentMatchesModel model buf

        }
        |> Prop.ofTestable


    override _.Run m =
        if byteCount = 0 then
            { m with totalReads = Array.empty :: m.totalReads }
        elif m.buffer.Length >= byteCount then
            { m with
                buffer = m.buffer[byteCount..]
                totalReads = m.buffer[.. byteCount - 1] :: m.totalReads }
        else
            let b = byteCount - m.buffer.Length

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

    override _.ToString() = $"Read {byteCount} bytes"

type PickSpanOp() =
    inherit FillOp()

    override _.Check(reader, model) =
        let is, buf = reader

        task {
            let! span = buf.pickSpan is

            return
                equalityProp "Buffer bytes match model" (span.ToArray()) model.buffer
                .&. bufferContentMatchesModel model buf
                .&. inputStreamMatchesModel model is
        }
        |> Prop.ofTestable

    override _.ToString() = "PickSpan"

type PickByteOp() =
    inherit FillOp()

    override _.Check(reader, model) =
        let is, buf = reader

        let byteMatchesModel =
            let expected = if Array.isEmpty model.buffer then None else Some model.buffer[0]
            equalityProp "Pick byte matches model" >> (|>) expected

        task {
            let! byte = buf.pickByte is

            return
                byteMatchesModel byte
                .&. bufferContentMatchesModel model buf
                .&. inputStreamMatchesModel model is
        }
        |> Prop.ofTestable

    override _.ToString() = "PickByte"


type CopyOp() =
    inherit Operation<BufferedReader, BufferedReadModel>()

    override _.Check(reader, model) =
        let is, buf = reader

        let copiedBytesMatchModel =
            equalityProp "Copied bytes match model" >> (|>) model.totalReads.Head

        let sourceStreamReadToCompletion =
            lazy (is.Position = is.Capacity)
            |> Prop.label "Source stream read to completion"

        task {
            use dst = new MemoryStream()

            do! buf.copy is dst

            return
                sourceStreamReadToCompletion
                .&. copiedBytesMatchModel (dst.GetBuffer())
                .&. bufferContentMatchesModel model buf
                .&. inputStreamMatchesModel model is
        }
        |> Prop.ofTestable

    override _.Run m =
        { m with
            input = Array.empty
            buffer = Array.empty
            inputReads = m.input :: m.inputReads
            totalReads = Array.append m.buffer m.input :: m.totalReads }



type ReaderSetup(bufferSize, source: byte array) =
    inherit Setup<BufferedReader, BufferedReadModel>()

    override _.Actual() =
        let is = new MemoryStream(source, 0, source.Length, false, true)
        let buffer = ReadStreamBuffer(Memory(Array.zeroCreate bufferSize))
        is, buffer

    override _.Model() =
        { input = source
          buffer = Array.empty
          bufferCapacity = bufferSize
          inputReads = List.empty
          totalReads = List.empty }

type ReaderMachine() =
    inherit Machine<BufferedReader, BufferedReadModel>()

    override _.Next _ =
        gen {
            let! readSize = seq { for n in 0..10 -> 2. ** n |> int } |> Gen.elements
            return! Gen.elements [ FillOp(); ReadOp(readSize); PickSpanOp(); PickSpanOp(); CopyOp() ]
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
