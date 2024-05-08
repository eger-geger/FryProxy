namespace FryProxy.IO.BufferedParser

/// Buffered parser expression builder.
type BufferedParserBuilder() =
    member _.Bind(parser, binder) = Parser.bind binder parser

    member _.Return a = Parser.unit a

    member _.Zero() = Parser.failed

[<AutoOpen>]
module Global =

    /// Buffered parser expression.
    let bufferedParser = BufferedParserBuilder()
