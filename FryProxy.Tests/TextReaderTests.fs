namespace FryProxy.Tests

open System
open System.Text
open NUnit.Framework
open System.IO

type TextReaderTests() =
    
    static member private toStringReader ([<ParamArray>] arr: String[]) =
        let acc = Seq.fold<string, StringBuilder> (fun sb -> sb.AppendLine) (StringBuilder()) arr
        new StringReader(acc.ToString())
    
    static member private readSeqTestCases() =
        seq {
            yield TestCaseData(TextReaderTests.toStringReader(Array.empty)).Returns(Seq.empty)
            yield TestCaseData(TextReaderTests.toStringReader("A")).Returns(["A"])
            yield TestCaseData(TextReaderTests.toStringReader("A", "B")).Returns(["A";"B"])
        }
    
    [<TestCaseSource("readSeqTestCases")>]
    member this.testToSeq reader =
        TextReader.toSeq reader

