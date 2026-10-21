module FryProxy.Tests.Http2.Hpack.FieldPackTests

open System.Buffers
open FryProxy.Http
open FryProxy.Http2.Hpack
open NUnit.Framework
open FsUnit

[<Literal>]
let DefaultTableSizeLimit = 4096u

let defaultTable = { Table.empty with SizeLimit = DefaultTableSizeLimit }

let sensitiveHeaderCases =
    seq {
        yield TestCaseData("authorization", "Bearer token123").SetName("Sensitive: authorization (lowercase)")
        yield TestCaseData("Authorization", "Basic dXNlcjpwYXNz").SetName("Sensitive: Authorization (TitleCase)")
        yield TestCaseData("AUTHORIZATION", "Bearer secret-token").SetName("Sensitive: AUTHORIZATION (UPPERCASE)")

        yield
            TestCaseData("proxy-authorization", "Basic cHJveHk6cGFzcw==")
                .SetName("Sensitive: proxy-authorization (lowercase)")

        yield
            TestCaseData("Proxy-Authorization", "Negotiate auth-data")
                .SetName("Sensitive: Proxy-Authorization (TitleCase)")

        yield
            TestCaseData("PROXY-AUTHORIZATION", "Basic proxy-pass")
                .SetName("Sensitive: PROXY-AUTHORIZATION (UPPERCASE)")

        yield TestCaseData("cookie", "session=abcdef123456").SetName("Sensitive: cookie (lowercase)")
        yield TestCaseData("Cookie", "id=1; theme=dark").SetName("Sensitive: Cookie (TitleCase)")
        yield TestCaseData("COOKIE", "auth_tracking=xyz").SetName("Sensitive: COOKIE (UPPERCASE)")
        yield TestCaseData("set-cookie", "sessionId=abc; Secure; HttpOnly").SetName("Sensitive: set-cookie (lowercase)")

        yield
            TestCaseData("Set-Cookie", "token=secret; Path=/; SameSite=Strict")
                .SetName("Sensitive: Set-Cookie (TitleCase)")

        yield TestCaseData("SET-COOKIE", "uid=999; Domain=example.com").SetName("Sensitive: SET-COOKIE (UPPERCASE)")

        yield
            TestCaseData("www-authenticate", "Basic realm=\"Access\"")
                .SetName("Sensitive: www-authenticate (lowercase)")

        yield
            TestCaseData("WWW-Authenticate", "Bearer realm=\"example\"")
                .SetName("Sensitive: WWW-Authenticate (TitleCase)")

        yield
            TestCaseData("WWW-AUTHENTICATE", "Digest realm=\"api\"").SetName("Sensitive: WWW-AUTHENTICATE (UPPERCASE)")

        yield
            TestCaseData("proxy-authenticate", "Basic realm=\"proxy\"")
                .SetName("Sensitive: proxy-authenticate (lowercase)")

        yield TestCaseData("Proxy-Authenticate", "Basic").SetName("Sensitive: Proxy-Authenticate (TitleCase)")
        yield TestCaseData("PROXY-AUTHENTICATE", "Negotiate").SetName("Sensitive: PROXY-AUTHENTICATE (UPPERCASE)")
        yield TestCaseData("x-api-key", "secret-api-key-12345").SetName("Sensitive: x-api-key (lowercase)")
        yield TestCaseData("X-Api-Key", "SECRET_KEY_999").SetName("Sensitive: X-Api-Key (TitleCase)")
        yield TestCaseData("X-API-KEY", "abcdef0123456789").SetName("Sensitive: X-API-KEY (UPPERCASE)")
        yield TestCaseData("x-auth-token", "jwt-token-string").SetName("Sensitive: x-auth-token (lowercase)")
        yield TestCaseData("X-Auth-Token", "header-token-val").SetName("Sensitive: X-Auth-Token (TitleCase)")
        yield TestCaseData("X-AUTH-TOKEN", "token-val-456").SetName("Sensitive: X-AUTH-TOKEN (UPPERCASE)")
    }

let nonSensitiveHeaderCases =
    seq {
        yield TestCaseData(":method", "GET").SetName("Non-sensitive: :method")
        yield TestCaseData(":scheme", "https").SetName("Non-sensitive: :scheme")
        yield TestCaseData(":path", "/index.html").SetName("Non-sensitive: :path")
        yield TestCaseData(":authority", "example.com").SetName("Non-sensitive: :authority")
        yield TestCaseData(":status", "200").SetName("Non-sensitive: :status")
        yield TestCaseData("content-type", "application/json").SetName("Non-sensitive: content-type")
        yield TestCaseData("content-length", "1024").SetName("Non-sensitive: content-length")
        yield TestCaseData("accept", "application/json, text/plain").SetName("Non-sensitive: accept")
        yield TestCaseData("user-agent", "Mozilla/5.0").SetName("Non-sensitive: user-agent")
        yield TestCaseData("cache-control", "no-cache").SetName("Non-sensitive: cache-control")
        yield TestCaseData("location", "https://example.com/login").SetName("Non-sensitive: location")
        yield TestCaseData("date", "Mon, 21 Oct 2024 12:00:00 GMT").SetName("Non-sensitive: date")
        yield TestCaseData("custom-header", "custom-value").SetName("Non-sensitive: custom-header")
        yield TestCaseData("x-request-id", "req-123-456").SetName("Non-sensitive: x-request-id")
    }

[<TestCaseSource(nameof sensitiveHeaderCases)>]
let testSensitiveHeaderOptionNeverIndexed (name: string, value: string) =
    let field = { Field.Name = name; Value = value }
    let packed = FieldPack.Default field
    let (FieldPack(fld, opts)) = packed

    fld |> should equal field
    opts |> should equal PackOpts.NeverIndexed

[<TestCaseSource(nameof nonSensitiveHeaderCases)>]
let testNonSensitiveHeaderOptionRawIndexed (name: string, value: string) =
    let field = { Field.Name = name; Value = value }
    let packed = FieldPack.Default field
    let (FieldPack(fld, opts)) = packed

    fld |> should equal field
    opts |> should equal PackOpts.RawIndexed

[<TestCaseSource(nameof sensitiveHeaderCases)>]
let testSensitiveHeaderBuildsNeverIndexedCommand (name: string, value: string) =
    let field = { Field.Name = name; Value = value }
    let packed = FieldPack.Default field
    let command = Table.buildCommand packed defaultTable

    match command with
    | NeverIndexedLiteralField _ -> ()
    | other -> Assert.Fail($"Expected NeverIndexedLiteralField, but got: {other}")
