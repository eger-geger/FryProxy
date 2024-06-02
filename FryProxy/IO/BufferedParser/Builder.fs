namespace FryProxy.IO.BufferedParser

/// Buffered parser expression builder.
type BufferedParserBuilder() =
    member _.Bind(parser, binder) = Parser.bind binder parser

    member _.Return a = Parser.unit a

    member _.ReturnFrom p = p

    // member _.Combine(action, fn) = Parser.bind (fun _ -> fn) action

    // member _.Delay(fn: unit -> Parser<'a>) = fn ()

    // member _.While(cond, p) = Parser.takeWhile cond p


[<AutoOpen>]
module Global =

    /// Buffered parser expression.
    let bufferedParser = BufferedParserBuilder()
