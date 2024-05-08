module FryProxy.Tests.IO.ReadBufferTests

open System
open System.IO
open FryProxy.IO
open FsCheck.Experimental
open FsCheck.FSharp
open FsCheck.NUnit

type BufferModel =
    { input: byte[]
      buffer: byte[]
      bufferCapacity: int
      inputReads: byte[] list
      totalReads: byte[] list }

type BufferOp = Operation<ReadBuffer<MemoryStream>, BufferModel>

/// <summary>
/// Consume given number of bytes to buffer.
/// </summary>
let fillBuffer (n: int) (m: BufferModel) =
    let read = m.input[.. n - 1]

    { m with
        input = m.input[n..]
        buffer = Array.append m.buffer read
        inputReads = read :: m.inputReads }


let equalityProp (desc: string) (act: 'a) (exp: 'a) =
    act = exp
    |> Prop.label (String.concat "\n\t" [ desc; $"act: %A{act}"; $"exp: %A{exp}" ])

/// Input stream unread bytes should match the model input.
let inputStreamMatchesModel (model: BufferModel) (is: MemoryStream) =
    let inputArr = is.GetBuffer()[(int is.Position) ..]

    equalityProp "Input matches model" inputArr model.input
    |> Prop.trivial (Array.isEmpty model.input)

/// Buffer content should match model buffer
let bufferContentMatchesModel (model: BufferModel) (buff: ReadBuffer<_>) =
    let buffArr = buff.Pending.ToArray()

    equalityProp "Buffer matches model" buffArr model.buffer
    |> Prop.trivial (Array.isEmpty model.buffer)


type FillOp() =
    inherit BufferOp()

    override _.Check(buff, model) =
        let byteCountMatchesModel =
            let expected = model.inputReads.Head.Length

            equalityProp "Number consumed bytes matches model"
            >> (|>) expected
            >> Prop.trivial (expected = 0)

        task {
            let! n = buff.Fill()

            return
                byteCountMatchesModel n
                .&. bufferContentMatchesModel model buff
                .&. inputStreamMatchesModel model buff.Stream
        }
        |> Prop.ofTestable

    override _.Run m =
        match m.bufferCapacity - m.buffer.Length, m.input.Length with
        | 0, _ -> { m with inputReads = Array.empty :: m.inputReads }
        | _, 0 -> { m with inputReads = Array.empty :: m.inputReads }
        | b, l when b <= l -> fillBuffer b m
        | _, l -> fillBuffer l m

    override _.ToString() = "Fill"

type PickSpanOp() =
    inherit FillOp()

    override _.Check(buff, model) =
        task {
            let! span = buff.Pick()

            return
                equalityProp "Buffer bytes match model" (span.ToArray()) model.buffer
                .&. bufferContentMatchesModel model buff
                .&. inputStreamMatchesModel model buff.Stream
        }
        |> Prop.ofTestable

    override _.ToString() = "Pick"

type CopyOp(n: uint64) =
    inherit BufferOp()

    override _.Check(buff, model) =
        let copiedBytesMatchModel =
            equalityProp "Copied bytes match model" >> (|>) model.totalReads.Head

        let sourceStreamReadToCompletion =
            lazy (buff.Stream.Position = buff.Stream.Capacity)
            |> Prop.label "Source stream read to completion"

        task {
            use dst = new MemoryStream()

            do! buff.Copy n dst

            return
                sourceStreamReadToCompletion
                .&. copiedBytesMatchModel (dst.GetBuffer())
                .&. bufferContentMatchesModel model buff
                .&. inputStreamMatchesModel model buff.Stream
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
    inherit BufferOp()

    override _.Pre m = m.buffer.Length >= n

    override _.Check(buff, model) =
        buff.Discard(n)

        bufferContentMatchesModel model buff
        .&. inputStreamMatchesModel model buff.Stream

    override _.Run m =
        { m with
            buffer = m.buffer[n..]
            inputReads = Array.empty :: m.inputReads
            totalReads = Array.empty :: m.totalReads }

    override _.ToString() = $"Discard({n})"

type ReaderSetup(bufferSize, source: byte array) =
    inherit Setup<ReadBuffer<MemoryStream>, BufferModel>()

    override _.Actual() =
        let is = new MemoryStream(source, 0, source.Length, false, true)
        ReadBuffer(Memory(Array.zeroCreate bufferSize), is)

    override _.Model() =
        { input = source
          buffer = Array.empty
          bufferCapacity = bufferSize
          inputReads = List.empty
          totalReads = List.empty }

type ReaderMachine() =
    inherit Machine<ReadBuffer<MemoryStream>, BufferModel>()

    override _.Next model =
        gen {
            let! discardN = Gen.choose (0, model.buffer.Length)

            return!
                Gen.elements
                    [ FillOp()
                      PickSpanOp()
                      PickSpanOp()
                      CopyOp(uint64 (model.buffer.Length + model.input.Length))
                      DiscardOp(discardN) ]
        }

    override _.Setup =
        let sizes = seq { for n in 1..5 -> 2. ** n |> int }

        let setup =
            gen {
                let! bufferSize = Gen.elements sizes
                let! source = Gen.choose (0, 255) |> Gen.map byte |> Gen.arrayOf |> Gen.scaleSize ((*) 32)
                return ReaderSetup(bufferSize, source) :> Setup<ReadBuffer<MemoryStream>, BufferModel>
            }

        Arb.fromGen setup


[<Property(Parallelism = 4)>]
let testReadStreamBuffer () =
    let machine = ReaderMachine()
    StateMachine.toProperty machine
