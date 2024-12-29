namespace FryProxy.Http.Hpack

open System
open FryProxy.Http

/// Field serialization options.
[<Flags>]
type PackOpts =
    | RawIndexed = 0uy
    | NotIndexed = 1uy
    | NeverIndexed = 2uy
    | HuffmanCoded = 4uy

/// A field along with its serialization options.
[<Struct>]
type FieldPack = FieldPack of Field * PackOpts

module FieldPack =

    let inline make opts fld = FieldPack(fld, opts)
    let inline Default fld = make PackOpts.RawIndexed fld
    let inline NotIndexed fld = make PackOpts.NotIndexed fld
    let inline NeverIndexed fld = make PackOpts.NeverIndexed fld
    let inline HuffmanCoded fld = make PackOpts.HuffmanCoded fld

/// Field stored in dynamic table.
[<Struct>]
type TableEntry = { Field: Field; Size: uint32 }

/// Dynamic indexing table.
[<Struct>]
type DynamicTable = { Entries: TableEntry List; SizeLimit: uint32 }
