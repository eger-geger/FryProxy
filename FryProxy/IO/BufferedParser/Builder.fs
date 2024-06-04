namespace FryProxy.IO.BufferedParser

/// Buffered parser expression builder.
type BufferedParserBuilder() =
    member inline _.Bind(parser, binder) = Parser.bind binder parser

    member inline _.Return a = Parser.unit a

    member inline _.ReturnFrom p = p


[<AutoOpen>]
module Global =

    /// Buffered parser expression.
    let bufferedParser = BufferedParserBuilder()
