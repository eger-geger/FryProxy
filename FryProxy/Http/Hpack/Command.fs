namespace FryProxy.Http.Hpack

open System
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
    let indexedFlag = 0b1000_0000uy

    [<Literal>]
    let incrementalFlag = 0b0100_0000uy

    [<Literal>]
    let tableSizeFlag = 0b0010_0000uy

    [<Literal>]
    let neverIndexFlag = 0b0001_0000uy

    let inline decodeFieldIndex prefix =
        decoder {
            let! num = NumericLit.decode prefix

            match NumericLit.toUint16 num with
            | Ok idx -> return idx
            | Error er -> return! Decoder.error er
        }

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

    let decodeIndexedField =
        decoder {
            let! idx = decodeFieldIndex 1
            return IndexedField idx
        }

    let decodeTableSize =
        decoder {
            let! num = NumericLit.decode 3

            match NumericLit.toUint16 num with
            | Ok idx -> return TableSize idx
            | Error er -> return! Decoder.error er
        }

    let decodeCommand: Command Decoder =
        decoder {
            let! cmdType = Decoder.peek

            if cmdType |> Flag.check indexedFlag then
                return! decodeIndexedField
            elif cmdType |> Flag.check incrementalFlag then
                return! decodeLiteralField 2 IndexedLiteralField
            elif cmdType |> Flag.check tableSizeFlag then
                return! decodeTableSize
            elif cmdType |> Flag.check neverIndexFlag then
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
