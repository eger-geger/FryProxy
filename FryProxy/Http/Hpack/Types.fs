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

    /// Construct packed field with given options.
    let inline make opts fld = FieldPack(fld, opts)

    /// Construct indexable packed field.
    let inline Default fld = make PackOpts.RawIndexed fld

    /// Construct packed field without storing it in dynamic table.
    let inline NotIndexed fld = make PackOpts.NotIndexed fld

    /// Construct packed field which should not be stored in dynamic table neither now nor by upstream hops.
    let inline NeverIndexed fld = make PackOpts.NeverIndexed fld

    /// Construct packed field which value and name (unless indexed) are encoded using static Huffman code.
    let inline HuffmanCoded fld = make PackOpts.HuffmanCoded fld

/// Field stored in dynamic table.
[<Struct>]
type TableEntry = { Field: Field; Size: uint32 }

/// Dynamic indexing table.
[<Struct>]
type DynamicTable = { Entries: TableEntry List; Size: uint32; SizeLimit: uint32 }
