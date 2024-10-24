[<AutoOpen>]
module FryProxy.Tests.Constraints.Operators

let matchResponse = ResponseEqualConstraint

let shouldThrowAsync<'T> = ThrowAsync(typeof<'T>)
