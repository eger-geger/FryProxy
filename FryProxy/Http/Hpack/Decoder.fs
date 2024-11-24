namespace FryProxy.Http.Hpack

[<Struct>]
type 'T DecodeResult =
    | DecVal of Value: 'T * Offset: int
    | DecErr of Message: string * Location: int

type 'T Decoder = int -> byte array -> 'T DecodeResult

module Decoder =

    let inline unit value i _ = DecVal(value, i)

    let inline error msg i _ = DecErr(msg, i)
    
    let inline valueAt i value = DecVal(value, i)
    
    let inline bind ([<InlineIfLambda>] fn: 'a -> 'b Decoder) (decoder: 'a Decoder) i bs =
        match decoder i bs with
        | DecVal(a, off) -> fn a off bs
        | DecErr(msg, loc) -> DecErr(msg, loc)

    let inline map ([<InlineIfLambda>] fn: 'a -> 'b) (decoder: 'a Decoder) i bs =
        match decoder i bs with
        | DecVal(a, off) -> DecVal(fn a, off)
        | DecErr(msg, loc) -> DecErr(msg, loc)

    let inline defaultValue a result =
        match result with
        | DecVal(v, _) -> v
        | DecErr _ -> a

    let inline peek i (bs: byte array) =
        match Array.tryItem i bs with
        | Some item -> DecVal(item, i)
        | None -> DecErr("byte sequence ended unexpectedly", i)

    let inline take i (bs: byte array) =
        match Array.tryItem i bs with
        | Some item -> DecVal(item, i + 1)
        | None -> DecErr("byte sequence ended unexpectedly", i)

    let inline takeArr len i (bs: byte array) =
        let j = i + len

        if bs.Length < j then
            DecErr($"insufficient number of bytes ({bs.Length - i}) < {len}", i)
        else
            DecVal(bs[i .. (j - 1)], j)

type DecoderBuilder() =

    member inline _.Bind(decoder, binder) = Decoder.bind binder decoder

    member inline _.Return a = Decoder.unit a

    member inline _.ReturnFrom decoder = decoder

[<AutoOpen>]
module Global =
    let decoder = DecoderBuilder()
