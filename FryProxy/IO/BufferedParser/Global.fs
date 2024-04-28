[<AutoOpen>]
module FryProxy.IO.BufferedParser.Global

open FryProxy.IO

/// Buffered parser expression.
let bufferedParser = BufferedParserBuilder()

/// Parser of UTF8 encoded line terminated with a line break (included).
let utf8LineParser: string Parser = Parser.parseBuffer ByteBuffer.tryTakeUTF8Line
