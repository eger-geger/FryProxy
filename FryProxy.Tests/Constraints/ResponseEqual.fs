namespace FryProxy.Tests.Constraints

open System.Net.Http
open System.Net.Http.Headers
open FryProxy.Http
open FryProxy.Http.Fields
open NUnit.Framework.Constraints
open FryProxy.Extension

type ResponseMatcher = HttpResponseMessage -> HttpResponseMessage -> bool

type ResponseEqualConstraint(expected: HttpResponseMessage) =
    inherit EqualConstraint(expected)

    let fuzzyFields =
        [ Connection.CloseField; FieldOf { TransferEncoding = [ "chunked" ] } ]

    let contentEquals (a: HttpContent) (b: HttpContent) =
        task {
            let! aBytes = a.ReadAsByteArrayAsync()
            let! bBytes = b.ReadAsByteArrayAsync()

            return aBytes = bBytes
        }
        |> (_.Result)


    let headerFieldsEqual (a: HttpResponseHeaders) (b: HttpResponseHeaders) =
        let isFuzzyField key () =
            (a.TryGetValues key |> ValueOption.ofAttempt)
            |> ValueOption.orElse(b.TryGetValues key |> ValueOption.ofAttempt)
            |> ValueOption.map(List.ofSeq >> Field.create key)
            |> ValueOption.map(List.contains >> (|>) fuzzyFields)
            |> ValueOption.get

        let compareValues a b =
            Set.difference <| Set.ofSeq a <| Set.ofSeq b |> Set.isEmpty

        let compareFields key =
            ValueOption.map2 compareValues
            <| (a.TryGetValues key |> ValueOption.ofAttempt)
            <| (b.TryGetValues key |> ValueOption.ofAttempt)
            |> ValueOption.defaultWith(isFuzzyField key)

        Seq.concat [ a; b ] |> Seq.map(_.Key) |> Seq.map compareFields |> Seq.forall id

    let matchWith matcher accessor : ResponseMatcher =
        fun a b -> matcher (accessor a) (accessor b)

    override this.ApplyTo<'TActual>(actual: 'TActual) : ConstraintResult =
        if actual.GetType().Equals typeof<HttpResponseMessage> |> not then
            upcast this.result actual false
        else
            actual :> obj :?> HttpResponseMessage |> this.compare |> (fun a -> upcast a)

    member private this.compare(actual: HttpResponseMessage) : EqualConstraintResult =
        let matchers =
            [ matchWith (=) (_.StatusCode)
              matchWith (=) (_.ReasonPhrase)
              matchWith headerFieldsEqual (_.Headers)
              matchWith (=) (_.TrailingHeaders.ToString())
              matchWith contentEquals (_.Content) ]

        matchers |> Seq.forall((||>)(actual, expected)) |> this.result actual

    member private this.result (actual: obj) success =
        EqualConstraintResult(this, actual, success)
