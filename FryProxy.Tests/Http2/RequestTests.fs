module FryProxy.Tests.Http2.RequestTests

open FryProxy.Http
open FryProxy.Http2
open NUnit.Framework

let validateFieldsCases () =
    let tc (name: string) (fields: Field list) (expected: Result<Field list, string>) =
        TestCaseData(fields).Returns(expected).SetName(name)

    let positive name fields = tc name fields (Ok fields)
    let negative name fields error = tc name fields (Error error)

    let invalidName name fieldName =
        negative name [ { Name = fieldName; Value = "value" } ]
        <| "HTTP/2 field names must be lowercase and use token characters."

    let invalidValue name fieldValue =
        negative name [ { Name = "x-test"; Value = fieldValue } ]
        <| "HTTP/2 field values contain invalid characters."

    let connectionField fieldName =
        negative $"rejects connection-specific field %s{fieldName}"
        <| [ { Name = fieldName; Value = "value" } ]
        <| "HTTP/2 messages must not contain connection-specific fields."

    let invalidTe name fieldValue =
        negative name [ { Name = "te"; Value = fieldValue } ]
        <| "HTTP/2 TE field value must be trailers."

    [ positive "accepts an empty field list" []

      positive "preserves valid fields and duplicates"
      <| [ { Name = ":method"; Value = "GET" }
           { Name = "x-test"; Value = "value" }
           { Name = "x-test"; Value = "other" } ]

      positive "accepts token characters and internal field-value whitespace"
      <| [ { Name = "!#$%&'*+-.^_`|~0123456789abcdefghijklmnopqrstuvwxyz"
             Value = "\u0021\u007E \t\u0080\u00FF" } ]

      positive "accepts an empty field value" [ { Name = "x-test"; Value = "" } ]

      positive "accepts TE trailers case-insensitively"
      <| [ { Name = "te"; Value = "TrAiLeRs" } ]

      invalidName "rejects uppercase field name" "X-Test"
      invalidName "rejects null field name" null
      invalidName "rejects empty field name" ""
      invalidName "rejects colon-only pseudo-header name" ":"
      invalidName "rejects extra leading colon" "::method"
      invalidName "rejects trailing colon" ":method:"
      invalidName "rejects non-token field name character" "x/test"

      invalidValue "rejects null field value" null
      invalidValue "rejects control character in field value" "\u0001"
      invalidValue "rejects DEL in field value" "\u007F"
      invalidValue "rejects field value above obs-text range" "\u0100"
      invalidValue "rejects leading field-value whitespace" " value"
      invalidValue "rejects trailing field-value whitespace" "value\t"
      invalidValue "rejects invalid final interior character" "ab\u0001c"

      invalidTe "rejects TE transfer coding" "gzip"
      invalidTe "rejects TE with multiple values" "trailers, gzip"
      invalidTe "rejects empty TE" ""

      negative "returns first error in field order"
      <| [ { Name = "te"; Value = "gzip" }; { Name = "X-Test"; Value = "value" } ]
      <| "HTTP/2 TE field value must be trailers."

      negative "checks field name before field value"
      <| [ { Name = "X-Test"; Value = null } ]
      <| "HTTP/2 field names must be lowercase and use token characters."

      negative "checks field value before connection-specific field"
      <| [ { Name = "connection"; Value = null } ]
      <| "HTTP/2 field values contain invalid characters."

      connectionField "connection"
      connectionField "keep-alive"
      connectionField "proxy-connection"
      connectionField "transfer-encoding"
      connectionField "upgrade" ]

[<TestCaseSource(nameof validateFieldsCases)>]
let testValidateFields (fields: Field list) = Request.validateFields fields

let tokenCases () =
    let tc (name: string) (offset: int) (value: string) (expected: bool) =
        TestCaseData(offset, value).Returns(expected).SetName(name)

    let positive name offset value = tc name offset value true
    let negative name offset value = tc name offset value false

    [ positive "accepts a complete token" 0 "method"
      positive "accepts a token starting at an offset" 1 ":method"
      negative "rejects an invalid token character" 0 "m:thod"
      negative "rejects a null token string" 0 null
      negative "rejects an empty token string" 0 ""
      negative "rejects an empty token suffix" 1 ":"
      negative "rejects an offset beyond the token string" 2 "x"
      negative "rejects a negative token offset" -1 "x" ]

[<TestCaseSource(nameof tokenCases)>]
let testIsToken (offset: int) (value: string) = Request.isToken offset value
