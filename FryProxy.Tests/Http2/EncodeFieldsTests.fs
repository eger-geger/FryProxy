module FryProxy.Tests.Http2.EncodeFieldsTests

open System
open System.Buffers
open System.IO
open FryProxy.IO.BufferedParser
open FryProxy.Http
open FryProxy.Http2
open FryProxy.Http2.Frames
open FryProxy.Http2.Hpack
open FsUnit
open NUnit.Framework

let private extractFragments (frames: Frame list) =
    frames
    |> List.choose (fun f ->
        match f.Body with
        | FrameBody.Headers h -> Option.Some h.FieldFragment
        | FrameBody.Continuation c -> Option.Some c.FieldFragment
        | _ -> Option.None)

let private mergeFieldData (frames: Frame list) =
    let buf = ArrayBufferWriter<byte>()

    for fieldData in extractFragments frames do
        buf.Write(fieldData.Span)

    buf.WrittenSpan

let private fieldDecodeOk fields table : Result<struct (FieldPack list * DynamicTable), string> =
    Ok struct (fields, table)

[<Test>]
let testEncodeFieldsSingleFrame () =
    task {
        let fields =
            [ FieldPack.Default { Field.Name = ":status"; Value = "200" }
              FieldPack.Default { Field.Name = "content-type"; Value = "application/json" } ]

        let writer = ArrayBufferWriter<byte>()
        let updatedTable = Encode.fields 1u Table.empty fields writer

        use stream = new MemoryStream(writer.WrittenMemory.ToArray())
        let! frames = Parser.runS (Parser.eager Parse.frame) stream

        frames |> should haveLength 1

        frames.Head.Header |> should equal
        <| { Length = uint32 (writer.WrittenCount - 9)
             Type = FrameType.HEADERS
             Flags = uint8 HeadersFlags.END_HEADERS
             StreamId = 1u }

        Table.decodeFields Table.empty (mergeFieldData frames)
        |> should equal (fieldDecodeOk fields updatedTable)
    }

[<Test>]
let testEncodeFieldsEmptyList () =
    task {
        let fields: FieldPack list = []
        let writer = ArrayBufferWriter<byte>()
        let updatedTable = Encode.fields 3u Table.empty fields writer

        use stream = new MemoryStream(writer.WrittenMemory.ToArray())
        let! frames = Parser.runS (Parser.eager Parse.frame) stream

        frames |> should haveLength 1

        frames.Head.Header |> should equal
        <| { Length = uint32 (writer.WrittenCount - 9)
             Type = FrameType.HEADERS
             Flags = uint8 HeadersFlags.END_HEADERS
             StreamId = 3u }

        Table.decodeFields Table.empty (mergeFieldData frames)
        |> should equal (fieldDecodeOk fields updatedTable)
    }

[<Test>]
let testEncodeFieldsContinuation () =
    task {
        let streamId = 7u

        let fields =
            [ for i in 1..400 -> FieldPack.NotIndexed { Field.Name = $"x-large-header-{i:D4}"; Value = String('b', 60) } ]

        let writer = ArrayBufferWriter<byte>()
        let updatedTable = Encode.fields streamId Table.empty fields writer

        use stream = new MemoryStream(writer.WrittenMemory.ToArray())
        let! frames = Parser.runS (Parser.eager Parse.frame) stream

        frames |> List.map _.Header |> should equal
        <| [ { Length = 16318u
               Type = FrameType.HEADERS
               Flags = 0uy
               StreamId = streamId }
             { Length = 16318u
               Type = FrameType.CONTINUATION
               Flags = 0uy
               StreamId = streamId }
             { Length = 164u
               Type = FrameType.CONTINUATION
               Flags = uint8 ContinuationFlags.END_HEADERS
               StreamId = streamId } ]

        Table.decodeFields Table.empty (mergeFieldData frames)
        |> should equal (fieldDecodeOk fields updatedTable)
    }

[<Test>]
let testEncodeFieldsDynamicTableProgression () =
    task {
        let fields1 =
            [ FieldPack.Default { Field.Name = ":method"; Value = "GET" }
              FieldPack.Default { Field.Name = ":scheme"; Value = "http" }
              FieldPack.Default { Field.Name = ":path"; Value = "/" }
              FieldPack.Default { Field.Name = ":authority"; Value = "www.example.com" } ]

        let writer1 = ArrayBufferWriter<byte>()
        let table1 = Encode.fields 1u Table.empty fields1 writer1

        use stream1 = new MemoryStream(writer1.WrittenMemory.ToArray())
        let! frames1 = Parser.runS (Parser.eager Parse.frame) stream1

        frames1 |> should haveLength 1

        frames1.Head.Header |> should equal
        <| { Length = uint32 (writer1.WrittenCount - 9)
             Type = FrameType.HEADERS
             Flags = uint8 HeadersFlags.END_HEADERS
             StreamId = 1u }

        Table.decodeFields Table.empty (mergeFieldData frames1)
        |> should equal (fieldDecodeOk fields1 table1)

        // reuse authority which was indexed in dynamic table
        let fields3 =
            [ FieldPack.Default { Field.Name = ":method"; Value = "GET" }
              FieldPack.Default { Field.Name = ":scheme"; Value = "http" }
              FieldPack.Default { Field.Name = ":path"; Value = "/" }
              FieldPack.Default { Field.Name = ":authority"; Value = "www.example.com" }
              FieldPack.Default { Field.Name = "cache-control"; Value = "no-cache" } ]

        let writer3 = ArrayBufferWriter<byte>()
        let table3 = Encode.fields 3u table1 fields3 writer3

        use stream3 = new MemoryStream(writer3.WrittenMemory.ToArray())
        let! frames3 = Parser.runS (Parser.eager Parse.frame) stream3

        frames3 |> should haveLength 1

        frames3.Head.Header |> should equal
        <| { Length = uint32 (writer3.WrittenCount - 9)
             Type = FrameType.HEADERS
             Flags = uint8 HeadersFlags.END_HEADERS
             StreamId = 3u }

        Table.decodeFields table1 (mergeFieldData frames3)
        |> should equal (fieldDecodeOk fields3 table3)
    }

[<Test>]
let testEncodeFieldsDifferentIndexingModes () =
    task {
        let fields =
            [ FieldPack.Default { Field.Name = ":status"; Value = "200" }
              FieldPack.NotIndexed { Field.Name = ":path"; Value = "/sample/path" }
              FieldPack.NeverIndexed { Field.Name = "authorization"; Value = "Bearer token123" }
              FieldPack.HuffmanCoded { Field.Name = "user-agent"; Value = "FryProxy/1.0" } ]

        let writer = ArrayBufferWriter<byte>()
        let updatedTable = Encode.fields 42u Table.empty fields writer

        use stream = new MemoryStream(writer.WrittenMemory.ToArray())
        let! frames = Parser.runS (Parser.eager Parse.frame) stream

        frames |> should haveLength 1

        frames.Head.Header |> should equal
        <| { Length = uint32 (writer.WrittenCount - 9)
             Type = FrameType.HEADERS
             Flags = uint8 HeadersFlags.END_HEADERS
             StreamId = 42u }

        Table.decodeFields Table.empty (mergeFieldData frames)
        |> should equal (fieldDecodeOk fields updatedTable)
    }
