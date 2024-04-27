namespace FryProxy.IO.BufferedParser

type BufferedParserBuilder() =
    member _.Bind(parser, binder) = Parser.bind binder parser

    member _.Return a = Parser.unit a

    member _.Zero() = Parser.failed


[<AutoOpen>]
module Builder =
    let bufferedParser = BufferedParserBuilder()
