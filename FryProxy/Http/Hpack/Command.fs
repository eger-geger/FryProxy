namespace FryProxy.Http.Hpack

open System
open Microsoft.FSharp.Core

[<Struct>]
type LiteralFieldName =
    | Indexed of Index: uint16
    | Literal of Literal: StringLit

/// An instruction from encoder to decoder on how to update the shared dynamic field table and interpret a field.
[<Struct>]
type Command =
    | TableSize of uint16
    | IndexedField of uint16
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

    let inline decodeFieldIndex prefix =
        decoder {
            let! num = NumericLit.decode prefix

            match NumericLit.toUint16 num with
            | Ok idx -> return idx
            | Error er -> return! Decoder.error er
        }

    let inline encodeLiteralField flag prefix (struct (name, value)) buf =
        let nLen =
            match name with
            | Indexed index -> NumericLit.encode prefix (uint64 index) buf
            | Literal literal ->
                do buf[0] <- 0uy
                1 + StringLit.encode literal (buf.Slice(1))

        let vLen = StringLit.encode value (buf.Slice(nLen))
        do buf[0] <- buf[0] ||| flag
        vLen + nLen

    let inline decodeLiteralField prefix ctor =
        decoder {
            match! decodeFieldIndex prefix with
            | 0us ->
                let! name = StringLit.decode
                let! value = StringLit.decode
                return ctor struct (Literal name, value)
            | idx ->
                let! value = StringLit.decode
                return ctor struct (Indexed idx, value)
        }

    let inline encodeIndexedField (idx: uint16) buf =
        let len = NumericLit.encode 1 (uint64 idx) buf
        do buf[0] <- buf[0] ||| IndexedFlag
        len

    let decodeIndexedField =
        decoder {
            let! idx = decodeFieldIndex 1
            return IndexedField idx
        }

    let inline encodeTableSize (size: uint16) buf =
        let len = NumericLit.encode 3 (uint64 size) buf
        do buf[0] <- buf[0] ||| TableSizeFlag
        len

    let decodeTableSize =
        decoder {
            let! num = NumericLit.decode 3

            match NumericLit.toUint16 num with
            | Ok idx -> return TableSize idx
            | Error er -> return! Decoder.error er
        }

    /// Encode single command.
    let inline encodeCommand cmd buf =
        match cmd with
        | TableSize size -> encodeTableSize size buf
        | IndexedField idx -> encodeIndexedField idx buf
        | IndexedLiteralField field -> encodeLiteralField IncrementalFlag 2 field buf
        | NonIndexedLiteralField field -> encodeLiteralField NonIndexedFlag 4 field buf
        | NeverIndexedLiteralField field -> encodeLiteralField NeverIndexFlag 4 field buf

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
    let rec private encodeBlockLoop commands buf len =
        match commands with
        | [] -> len
        | cmd :: tail ->
            let cmdL = encodeCommand cmd buf
            encodeBlockLoop tail (buf.Slice(cmdL)) (cmdL + len)

    /// Encode command sequence into buffer.
    let encodeBlock commands buf = encodeBlockLoop commands buf 0

    [<TailCall>]
    let rec private decodeBlockLoop size acc (tail: byte ReadOnlySpan) =
        if tail.IsEmpty then
            DecoderResult(Ok(List.rev acc), size)
        else
            match decodeCommand.Invoke tail with
            | Ok cmd, n -> decodeBlockLoop (size + n) (cmd :: acc) (tail.Slice(int n))
            | Error e, n -> DecoderResult(Error e, n)

    /// Decode command sequence from a buffer.
    let decodeBlock bytes = decodeBlockLoop 0us List.Empty bytes
