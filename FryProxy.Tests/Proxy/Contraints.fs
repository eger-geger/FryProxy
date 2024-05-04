namespace FryProxy.Tests.Proxy

open System.Net.Http
open NUnit.Framework.Constraints

type ResponseMatcher = HttpResponseMessage -> HttpResponseMessage -> bool

type ResponseEqualConstraint(expected: HttpResponseMessage) =
    inherit EqualConstraint(expected)

    let contentEquals (a: HttpContent) (b: HttpContent) =
        task {
            let! aBytes = a.ReadAsByteArrayAsync()
            let! bBytes = b.ReadAsByteArrayAsync()

            return aBytes = bBytes
        }
        |> (_.Result)


    let matchWith matcher accessor : ResponseMatcher =
        fun a b -> matcher (accessor a) (accessor b)

    override this.ApplyTo<'TActual>(actual: 'TActual) : ConstraintResult =
        if actual.GetType().Equals typeof<HttpResponseMessage> |> not then
            upcast this.result false
        else
            actual :> obj :?> HttpResponseMessage |> this.compare |> (fun a -> upcast a)

    member private this.compare(actual: HttpResponseMessage) : EqualConstraintResult =
        let matchers =
            [ matchWith (=) (_.StatusCode)
              matchWith (=) (_.ReasonPhrase)
              matchWith (=) (_.Headers.ToString())
              matchWith (=) (_.TrailingHeaders.ToString())
              matchWith contentEquals (_.Content) ]

        matchers |> Seq.forall ((||>) (actual, expected)) |> this.result

    member private this.result success =
        EqualConstraintResult(this, expected, success)


[<AutoOpen>]
module Operators =
    
    let matchResponse = ResponseEqualConstraint