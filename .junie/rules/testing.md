# Test Scope & Iteration

- Increase test coverage incrementally while iterating on new code.
- Start with the simplest possible smoke test only after the core implementation has been reviewed.
- Expand test coverage and edge cases once the functional interface and behavior have settled.

# Test Assertions & F# Idioms

- Prefer comparing complete values (records, lists, structs) for equality rather than individual properties.
- When comparing discriminated unions or Result types, compare directly against the expected union member / constructor
  (e.g., `Ok struct (fields, table)`).
- Prefer asserting directly in pipeline expressions (`actual |> should equal <| expected`) rather than introducing
  intermediate variables solely for expected values.

# Test Design & Architecture

- Prefer data-driven tests (`[<TestCase>]` / `[<TestCaseSource>]`) for pure, stateless functions.
- Use separate, focused test cases when mutable state or side effects are involved (e.g., writing to buffers,
  streaming).
- Prefer end-to-end round-trip assertions (e.g., encode -> parse/decode) where parser/decoder pairs exist.
- Mirror production module hierarchy in test modules and separate distinct feature suites into dedicated test files.

# Fluent Test Cases

- For a pure function with many scenarios, define a local `TestCaseData` factory that accepts a readable case name, the
  function input, and the expected result. Set `TestName` and `ExpectedResult` on the generated case.
- Add small helpers for common outcomes (for example, positive and negative cases) so each case states only its
  scenario-specific input and expected values.
- Write cases as fluent, multiline applications: put the case name first, then the input, followed by the expected
  result (or the fields that form it). Use `<|` to keep long lists and records readable without obscuring that order.
- For long expected byte lists, split the bytes into meaningful segments (for example, frame header, payload, and
  padding) as separate list literals joined with `@`. Put the first segment after `<|` and align each continuation
  `@ [ ... ]` line with it.
- With NUnit's `ExpectedResult`, make the `[<TestCaseSource>]` test function return the function's actual result
  directly; NUnit compares it with the case's expected result. Prefer comparing complete results, including option/
  `voption` and record values.

# Working with Byte Buffers & Streams

- Use `System.Buffers.ArrayBufferWriter<byte>` by default as the substitute for `System.Buffers.IBufferWriter<byte>`.
- Manage buffer writers and streams locally inside the test body to avoid shared mutable state between test runs.
