namespace FryProxy.Http2.Hpack

open System
open Microsoft.FSharp.Core

/// Indexed or literal name of a literal field.
[<Struct>]
type LiteralFieldName =
    | Indexed of Index: uint32
    | Literal of Literal: StringLit

/// An instruction from encoder to decoder on how to update the shared dynamic field table and interpret a field.
[<Struct>]
type Command =
    | TableSize of uint32
    | IndexedField of uint32
    | IndexedLiteralField of Field: struct (LiteralFieldName * StringLit)
    | NonIndexedLiteralField of Field: struct (LiteralFieldName * StringLit)
    | NeverIndexedLiteralField of Field: struct (LiteralFieldName * StringLit)

module Command =

    [<Literal>]
    let IndexedFlag = 0b1000_0000uy

    [<Literal>]
    let IncrementalFlag = 0b0100_0000uy

    [<Literal>]
    let TableSizeFlag = 0b0010_0000uy

    [<Literal>]
    let NeverIndexFlag = 0b0001_0000uy

    [<Literal>]
    let NonIndexedFlag = 0uy

    /// Reference a field fully defined by table index.
    let inline indexedField i = i |> uint32 |> IndexedField

    /// Create literal field with indexed name and literal value of given kind.
    let inline literalIndexedField (kind: string -> StringLit) i value =
        struct (i |> uint32 |> Indexed, kind value)

    /// Create literal field with both name and value being string literal of given kind.
    let inline literalStringField kind name value =
        struct (name |> kind |> Literal, value |> kind)

    let inline private literalFieldNameCapacity (name: LiteralFieldName) =
        match name with
        | Indexed _ -> NumericLit.capacity
        | Literal lit -> StringLit.capacity lit

    let inline decodeFieldIndex prefix =
        decoder {
            let! num = NumericLit.decode prefix
            return NumericLit.toUint32 num
        }

    let inline encodeLiteralField flag prefix (struct (name, value)) (buf: byte Span) =
        if (literalFieldNameCapacity name) + (StringLit.capacity value) > buf.Length then
            ValueNone
        else
            let nameLen =
                match name with
                | Indexed index -> NumericLit.encode prefix index buf
                | Literal literal ->
                    do buf[0] <- 0uy
                    1 + StringLit.encode literal (buf.Slice(1))

            let valueLen = StringLit.encode value (buf.Slice(nameLen))
            do buf[0] <- buf[0] ||| flag
            valueLen + nameLen |> ValueSome

    let inline decodeLiteralField prefix ctor =
        decoder {
            match! decodeFieldIndex prefix with
            | 0u ->
                let! name = StringLit.decode
                let! value = StringLit.decode
                return ctor struct (Literal name, value)
            | idx ->
                let! value = StringLit.decode
                return ctor struct (Indexed idx, value)
        }

    let inline encodeIndexedField idx (buf: byte Span) =
        if buf.Length < NumericLit.capacity then
            ValueNone
        else
            let len = NumericLit.encode 1 idx buf
            do buf[0] <- buf[0] ||| IndexedFlag
            ValueSome len

    let decodeIndexedField =
        decoder {
            let! idx = decodeFieldIndex 1
            return IndexedField idx
        }

    let inline encodeTableSize size (buf: byte Span) =
        if buf.Length < NumericLit.capacity then
            ValueNone
        else
            let len = NumericLit.encode 3 size buf
            do buf[0] <- buf[0] ||| TableSizeFlag
            ValueSome len

    let decodeTableSize =
        decoder {
            let! num = NumericLit.decode 3
            return TableSize <| NumericLit.toUint32 num
        }

    /// Encode single command.
    let inline encode cmd buf =
        match cmd with
        | TableSize size -> encodeTableSize size buf
        | IndexedField idx -> encodeIndexedField idx buf
        | IndexedLiteralField field -> encodeLiteralField IncrementalFlag 2 field buf
        | NonIndexedLiteralField field -> encodeLiteralField NonIndexedFlag 4 field buf
        | NeverIndexedLiteralField field -> encodeLiteralField NeverIndexFlag 4 field buf


    /// Decode a single binary command.
    let decodeCommand: Command Decoder =
        decoder {
            let! cmdType = Decoder.peek

            if cmdType |> Flag.check IndexedFlag then
                return! decodeIndexedField
            elif cmdType |> Flag.check IncrementalFlag then
                return! decodeLiteralField 2 IndexedLiteralField
            elif cmdType |> Flag.check TableSizeFlag then
                return! decodeTableSize
            elif cmdType |> Flag.check NeverIndexFlag then
                return! decodeLiteralField 4 NeverIndexedLiteralField
            else
                return! decodeLiteralField 4 NonIndexedLiteralField
        }


    [<TailCall>]
    let rec private decodeBlockLoop size acc (tail: byte ReadOnlySpan) =
        if tail.IsEmpty then
            DecoderResult(Ok(List.rev acc), size)
        else
            match decodeCommand.Invoke tail with
            | Ok cmd, n -> decodeBlockLoop (size + n) (cmd :: acc) (tail.Slice(int n))
            | Error e, n -> DecoderResult(Error e, n)

    /// Decode command sequence from a buffer.
    let decodeBlock octets = decodeBlockLoop 0u List.Empty octets
