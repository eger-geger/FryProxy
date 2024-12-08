namespace FryProxy.Http.Hpack

[<Struct>]
type LiteralFieldName =
    | Indexed of Index: uint
    | Literal of Literal: string

[<Struct>]
type LiteralField = { Name: LiteralFieldName; Value: string }

/// An instruction from encoder to decoder on how to update the shared dynamic field table and interpret a field.
[<Struct>]
type Command =
    | TableSize of Update: uint16
    | IndexedField of Index: uint
    | IndexedLiteralField of Indexed: LiteralField
    | NonIndexedLiteralField of NonIndexed: LiteralField
    | NeverIndexedLiteralField of NeverIndexed: LiteralField

module Command =

    [<Literal>]
    let indexedFlag = 0b1000_0000uy

    [<Literal>]
    let incrementalFlag = 0b0100_0000uy

    [<Literal>]
    let tableSizeFlag = 0b0010_0000uy

    [<Literal>]
    let neverIndexFlag = 0b0001_0000uy

    let inline hasFlag flag value = flag &&& value = flag

    let inline decodeFieldIndex prefix =
        decoder {
            let! num = NumericLit.decode prefix

            match NumericLit.uint32 num with
            | Ok idx -> return idx
            | Error er -> return! Decoder.error er
        }

    let inline decodeLiteralField prefix ctor =
        decoder {
            match! decodeFieldIndex prefix with
            | 0u ->
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

            match NumericLit.uint16 num with
            | Ok idx -> return TableSize idx
            | Error er -> return! Decoder.error er
        }

    let decodeCommand: Command Decoder =
        decoder {
            let! cmdType = Decoder.peek

            if cmdType |> hasFlag indexedFlag then
                return! decodeIndexedField
            elif cmdType |> hasFlag incrementalFlag then
                return! decodeLiteralField 2 IndexedLiteralField
            elif cmdType |> hasFlag tableSizeFlag then
                return! decodeTableSize
            elif cmdType |> hasFlag neverIndexFlag then
                return! decodeLiteralField 4 NeverIndexedLiteralField
            else
                return! decodeLiteralField 4 NonIndexedLiteralField
        }

    let decodeBlock (block: byte array) : Command List DecodeResult =
        let rec loop i acc =
            if i = block.Length then
                DecVal(List.rev acc, i)
            else
                match decodeCommand i block with
                | DecVal(cmd, j) -> cmd :: acc |> loop j
                | DecErr(err, j) -> DecErr(err, j)

        loop 0 List.Empty
