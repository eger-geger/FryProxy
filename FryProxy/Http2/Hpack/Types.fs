namespace FryProxy.Http2.Hpack

open System
open System.Collections.Generic
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

    /// Standard set of default sensitive header names (case-insensitive)
    let defaultSensitiveHeaders =
        HashSet<string>(
            [| "authorization"
               "proxy-authorization"
               "cookie"
               "set-cookie"
               "www-authenticate"
               "proxy-authenticate"
               "x-api-key"
               "x-auth-token" |],
            StringComparer.OrdinalIgnoreCase
        )

    // Extract a raw field from the packed representaion.
    let inline field (FieldPack(f, _)) = f

    /// Construct packed field with given options.
    let inline make opts fld = FieldPack(fld, opts)

    /// Construct indexable packed field unless it is a sensitive header.
    let inline Default fld =
        let opt =
            if defaultSensitiveHeaders.Contains(fld.Name) then
                PackOpts.NeverIndexed
            else
                PackOpts.RawIndexed

        make opt fld

    // Construct packed field which should be stored in dynamic table.
    let inline RawIndexed fld = make PackOpts.RawIndexed fld

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
type DynamicTable = { Entries: TableEntry list; Size: uint32; SizeLimit: uint32 }
