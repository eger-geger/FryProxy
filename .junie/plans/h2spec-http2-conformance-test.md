---
sessionId: session-261028-171709-rq4y
---

# Requirements

### Overview
Add h2spec HTTP/2 conformance testing for FryProxy. h2spec (`summerwind/h2spec`) is an industry-standard tool that validates HTTP/2 server implementations against RFC 7540/7541. The test will be added directly to `ProxyTests.fs`, sharing the already-running proxy and wiremock infrastructure, but running the h2spec container on its own within the test body.

### Goals
- Validate FryProxy's HTTP/2 server implementation with h2spec conformance tests
- Run as a NUnit test locally via `dotnet test --filter "Category=h2spec&Explicit=true"`
- Run as a separate GitHub Actions step, publishing a JUnit XML report
- h2spec failures should **not** fail the CI build (report-only)
- h2spec tests are **excluded by default** from `dotnet test` (marked with `[<Explicit>]` and `[<Category("h2spec")>]`)
- Test is in `ProxyTests.fs`, sharing proxy and wiremock infrastructure, but runs the h2spec container on its own

### Scope
**In Scope:**
- Wiremock mappings for `GET /h2spec` and `POST /h2spec` returning 200 with non-empty body (required by h2spec)
- New h2spec test in `ProxyTests.fs` to access the already-running `opaqueProxy` and `WiremockFixture`, running the h2spec container on its own
- GitHub Actions step to run h2spec and publish the JUnit XML report
- Local execution via `dotnet test`

**Out of Scope:**
- TLS/HTTPS h2spec tests (h2spec supports `-t --tls`, but FryProxy doesn't expose HTTPS yet)
- Strict mode h2spec tests (`-S --strict`) — we'll start with the default test set
- Fixing any h2spec failures discovered (that's a follow-up task)

# Technical Design

### Current Implementation
- `WiremockFixture.fs` — `[<SetUpFixture>]` class that starts wiremock via Testcontainers, maps `FryProxy.Tests/wiremock/` to the container. Used by `ProxyTests.fs`.
- `ProxyTests.fs` — module (not a class) that creates `opaqueProxy` and `transparentProxy` as module-level `let` bindings, then starts/stops them in `[<OneTimeSetUp>]`/`[<OneTimeTearDown>]` module-level functions. Tests reference these proxies directly.
- `Settings.fs` — `Port = 0u` means pick any available port. `HttpProxy.Start()` begins listening.
- CI workflow (`dotnet.yaml`) — builds, runs `dotnet test`, reports TRX via `dorny/test-reporter`.

### Key Decisions
1. **h2spec test in ProxyTests.fs** — The h2spec test is added directly to `ProxyTests.fs`, so it has access to module-level bindings like `opaqueProxy`, `transparentProxy`, and `WiremockFixture`. The test only starts and stops the h2spec container within the test body — no separate proxy or wiremock setup.
2. **h2spec runs in a Docker container via Testcontainers** — Testcontainers' `ContainerBuilder` can run any image. We'll use `summerwind/h2spec:latest` with `WithCommand` to pass h2spec arguments. The container needs network access to the proxy via `host.testcontainers.internal`.
3. **JUnit XML report** — h2spec's `--junit-report` flag writes a JUnit XML file. We'll copy the file from the container. For CI, we'll use `dorny/test-reporter` with the `junit` reporter.
4. **Non-failing in CI** — The GitHub Actions step will use `continue-on-error: true` so h2spec failures don't block the pipeline.
5. **Test exclusion by default** — The h2spec test will be marked with both `[<Explicit>]` and `[<Category("h2spec")>]`. The `[<Explicit>]` attribute ensures the test is skipped in default `dotnet test` runs. CI must explicitly include it with `--filter "Category=h2spec&Explicit=true"`.

### Proposed Changes

#### 1. Add `/h2spec` wiremock mappings
- `FryProxy.Tests/wiremock/mappings/get-h2spec.json` — GET `/h2spec` returning 200 with a small body (already created).
- `FryProxy.Tests/wiremock/mappings/post-h2spec.json` — POST `/h2spec` returning 200 with a small body (new).

#### 2. Add h2spec test to `ProxyTests.fs`
- Add the h2spec test at the end of `FryProxy.Tests/Proxy/ProxyTests.fs`
- The test has access to module-level bindings like `opaqueProxy` and `WiremockFixture`.
- The test only starts and stops the h2spec container within the test body — no separate proxy or wiremock setup.
- Test body:
  - Reads the proxy port from the shared `opaqueProxy` instance.
  - Starts an h2spec container (`summerwind/h2spec:latest`) via Testcontainers, targeting `host.testcontainers.internal` at the proxy's port with `--path /h2spec` and `--junit-report` to a mounted path.
  - Waits for h2spec to complete, captures exit code and logs.
  - Copies the JUnit XML report from the container.
  - Stops the h2spec container.
- `[<Explicit>]` and `[<Category("h2spec")>]` attributes on the test to exclude from default test runs.
- Test passes regardless of h2spec exit code (failures are expected and will be addressed separately).
- Delete `H2SpecTests.fs` (no longer needed).

#### 3. Update CI workflow
- Add a step in `.github/workflows/dotnet.yaml`:
  - Runs `dotnet test --filter "Category=h2spec&Explicit=true"` to explicitly include h2spec tests.
  - Use `continue-on-error: true` so h2spec failures don't block the pipeline.
  - Add `dorny/test-reporter` step with `reporter: junit` to publish the JUnit XML report from h2spec.

### Architecture Diagram

``` mermaid
graph LR
  subgraph "ProxyTests.fs"
    PT[ProxyTests module]
    PTSetup[OneTimeSetUp/Teardown]
    T[H2Spec test]
  end
  subgraph "Docker"
    WM[Wiremock Container]
    HS[h2spec Container]
  end
  subgraph "Host"
    PX[FryProxy (opaque)]
  end
  WM -->|Started by| WiremockFixture[WiremockFixture]
  PTSetup -->|Starts| PX
  T -->|Uses| PX
  T -->|Runs| HS
  HS -->|HTTP/2 tests| PX
  PX -->|HTTP/1.1| WM
  HS -->|JUnit XML| T
```

### File Structure
```
FryProxy.Tests/
  Proxy/
    ProxyTests.fs           (modified — add h2spec test)
    WiremockFixture.fs      (unchanged)
  wiremock/mappings/
    get-h2spec.json         (exists — GET mapping)
    post-h2spec.json        (new — POST mapping)
  FryProxy.Tests.fsproj     (modified — remove H2SpecTests.fs reference)
.github/workflows/
  dotnet.yaml               (modified — add h2spec step)
```

# Delivery Steps

### ✓ Step 1: Add GET and POST `/h2spec` wiremock mappings
Create the wiremock mappings h2spec needs to run its tests.
- `get-h2spec.json` already exists (GET mapping). Verify it returns 200 with a non-empty body.
- Add `FryProxy.Tests/wiremock/mappings/post-h2spec.json` for POST `/h2spec` returning 200 with a small text body.
- Both mappings follow the same format as `get-example.org.json`.

### ✓ Step 2: Add h2spec test to `ProxyTests.fs` and clean up
Add the h2spec conformance test to `ProxyTests.fs`, reusing existing proxy and wiremock infrastructure, and remove the separate `H2SpecTests.fs` file.
- Add the h2spec test at the end of `FryProxy.Tests/Proxy/ProxyTests.fs`:
  - The test has access to module-level bindings like `opaqueProxy` and `WiremockFixture`.
  - Add necessary `open` statements for Testcontainers (`DotNet.Testcontainers.Builders`, `DotNet.Testcontainers.Configurations`).
  - Test body:
    - Reads the proxy port from the shared `opaqueProxy` instance.
    - Builds and starts an h2spec container (`summerwind/h2spec:latest`) with command arguments: `--host host.testcontainers.internal --port <proxy_port> --path /h2spec --junit-report /tmp/h2spec-report.xml`.
    - Waits for h2spec to complete, captures exit code and logs.
    - Copies the JUnit XML report from the container to the host.
    - Stops the h2spec container.
  - Mark with `[<Test>]`, `[<Explicit>]`, and `[<Category("h2spec")>]` attributes.
  - Test passes regardless of h2spec exit code (failures are expected and will be addressed separately).
- Delete `FryProxy.Tests/Proxy/H2SpecTests.fs` (no longer needed).
- Update `FryProxy.Tests.fsproj` to remove the `H2SpecTests.fs` reference.

### ✓ Step 3: Add h2spec CI step and report publishing
Wire up h2spec in GitHub Actions as a non-failing step with report publishing.
- Update `.github/workflows/dotnet.yaml`:
  - Add a step in the existing `ci` job that runs `dotnet test --filter "Category=h2spec&Explicit=true"` to explicitly include h2spec tests.
  - Use `continue-on-error: true` so h2spec failures don't block the pipeline.
  - Add `dorny/test-reporter` step with `reporter: junit` to publish the JUnit XML report from h2spec.
  - Ensure Docker is available in the runner (ubuntu-24.04 has it by default).