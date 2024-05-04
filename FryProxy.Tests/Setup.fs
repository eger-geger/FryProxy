namespace FryProxy.Tests

open FsUnit.TopLevelOperators
open NUnit.Framework

[<SetUpFixture>]
type InitFsUnitFormatter() =
    inherit FSharpCustomMessageFormatter()