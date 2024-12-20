namespace FryProxy.Http.Hpack

open System
open FryProxy.Extension
open Microsoft.FSharp.Core

[<Struct>]
type LiteralFieldName =
    | Indexed of Index: uint16
    | Literal of Literal: string

[<Struct>]
type LiteralField = { Name: LiteralFieldName; Value: string }

/// An instruction from encoder to decoder on how to update the shared dynamic field table and interpret a field.
[<Struct>]
type Command =
    | TableSize of uint16
    | IndexedField of uint16
    | IndexedLiteralField of Field: LiteralField
    | NonIndexedLiteralField of Field: LiteralField
    | NeverIndexedLiteralField of Field: LiteralField

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

    let encodeLiteralField flag prefix { Name = name; Value = value } =
        let valOct = StringLit.encodeRaw value

        match name with
        | Indexed index ->
            let idxOct = NumericLit.encode prefix (uint64 index)
            idxOct[0] <- idxOct[0] ||| flag
            Span.concat3 idxOct valOct.Len valOct.Str
        | Literal literal ->
            let idxOct = Stackalloc.span 1
            idxOct[0] <- flag
            let keyOct = StringLit.encodeRaw literal
            Span.concat5 idxOct keyOct.Len keyOct.Str valOct.Len valOct.Str

    let inline decodeLiteralField prefix ctor =
        decoder {
            match! decodeFieldIndex prefix with
            | 0us ->
                let! name = StringLit.decode
                let! value = StringLit.decode
                return ctor { Name = Literal name; Value = value }
            | idx ->
                let! value = StringLit.decode
                return ctor { Name = Indexed idx; Value = value }
        }

    let inline encodeIndexedField (idx: uint16) =
        let buf = NumericLit.encode 1 (uint64 idx)
        buf[0] <- buf[0] ||| IndexedFlag
        buf

    let decodeIndexedField =
        decoder {
            let! idx = decodeFieldIndex 1
            return IndexedField idx
        }

    let inline encodeTableSize (size: uint16) =
        let buf = NumericLit.encode 3 (uint64 size)
        buf[0] <- buf[0] ||| TableSizeFlag
        buf

    let decodeTableSize =
        decoder {
            let! num = NumericLit.decode 3

            match NumericLit.toUint16 num with
            | Ok idx -> return TableSize idx
            | Error er -> return! Decoder.error er
        }
    
    
    /// Encode single command
    let inline encodeCommand cmd : byte Span =
        match cmd with
        | TableSize size -> encodeTableSize size
        | IndexedField idx -> encodeIndexedField idx
        | IndexedLiteralField field -> encodeLiteralField IncrementalFlag 2 field
        | NonIndexedLiteralField field -> encodeLiteralField NonIndexedFlag 4 field
        | NeverIndexedLiteralField field -> encodeLiteralField NeverIndexFlag 4 field

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

    let decodeBlock bytes = decodeBlockLoop 0us List.Empty bytes
