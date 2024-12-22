module FryProxy.Tests.Http.Hpack.Hex

open System
open System.Buffers

type OctetWriter = delegate of byte Span -> int

type hex = string

let decodeArr (hex: hex) =
    let rec loop (s: string) acc =
        if String.IsNullOrEmpty s then
            acc |> List.rev
        else
            Convert.ToByte(s[0..1], 16) :: acc |> loop s[2..]

    loop (hex.Replace(" ", "")) [] |> List.toArray

let decodeSpan hex = ReadOnlySpan(decodeArr hex)

/// Convert a byte sequence into 'readable' space-separated hex string.
let encodeSeq (bytes: byte seq) : hex =
    bytes
    |> Seq.map(sprintf "%02x")
    |> Seq.chunkBySize 2
    |> Seq.map(String.Concat)
    |> Seq.reduce(sprintf "%s %s")

/// Execute octet writer on a temporary buffer and written octets as hex string.
let runWriter (wr: OctetWriter) =
    use mem = MemoryPool.Shared.Rent(0xffff)
    let buf = mem.Memory.Span
    let len = wr.Invoke(buf)
    buf.Slice(0, len).ToArray() |> encodeSeq
