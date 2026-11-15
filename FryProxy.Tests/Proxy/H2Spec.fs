module FryProxy.Tests.Proxy.H2Spec

open System
open System.IO
open System.Xml.Linq
open DotNet.Testcontainers.Builders
open NUnit.Framework

let updateElementAttribute (name: XName) (value: string) (el: XElement) =
    let attr = el.Attribute(name)

    if attr = null then
        el.Add(XAttribute(name, value))
    elif String.IsNullOrWhiteSpace(attr.Value) then
        attr.Value <- value

let private fixTestcaseName (testName: XName) (className: XName) (tc: XElement) =
    let classAttr = tc.Attribute(className)

    if classAttr <> null && not (String.IsNullOrWhiteSpace(classAttr.Value)) then
        do updateElementAttribute testName classAttr.Value tc

/// Fixes h2spec JUnit XML reports so that dorny/test-reporter@v3 can parse them.
/// h2spec generates reports with:
/// - Embedded null bytes (0x00) from raw binary data in error messages, making the XML invalid.
/// - Missing or empty `name` attributes on <testcase> elements, which cause
///   the parser to crash with `TypeError: Cannot read properties of undefined (reading 'trim')`.
let fixH2SpecJUnitReport (path: string) =
    let raw = File.ReadAllBytes(path)
    let clean = Array.filter ((<>) 0uy) raw
    File.WriteAllBytes(path, clean)

    let doc = XDocument.Load(path)
    let nameXName = XName.Get("name")
    let classXName = XName.Get("classname")

    doc.Descendants(XName.Get("testcase"))
    |> Seq.iter (fixTestcaseName nameXName classXName)

    doc.Save(path)

let runGroup host (port: int) ct (groups: string) =
    task {
        let safeName = groups.Replace("/", "_").Replace(" ", "_")
        let containerReportPath = $"/tmp/h2spec-{safeName}.xml"

        let h2SpecReportDir =
            Path.Combine(TestContext.CurrentContext.WorkDirectory, "h2spec")

        let hostReportPath = Path.Combine(h2SpecReportDir, $"h2spec-{safeName}.xml")

        let args =
            groups.Split(' ', StringSplitOptions.RemoveEmptyEntries)
            |> Array.append [| "--host"; host |]
            |> Array.append [| "--port"; string port |]
            |> Array.append [| "--path"; "/h2spec" |]
            |> Array.append [| "--timeout"; "3" |]
            |> Array.append [| "--junit-report"; containerReportPath |]

        let container =
            ContainerBuilder("summerwind/h2spec:latest").WithCommand(args).Build()

        do! container.StartAsync(ct)

        let! exitCode = container.GetExitCodeAsync()
        let! stdout, stderr = container.GetLogsAsync()

        do! TestContext.Out.WriteAsync(stdout)
        do! TestContext.Error.WriteAsync(stderr)

        Directory.CreateDirectory(h2SpecReportDir) |> ignore

        try
            let! reportBytes = container.ReadFileAsync(containerReportPath, ct)
            do! File.WriteAllBytesAsync(hostReportPath, reportBytes, ct)
            fixH2SpecJUnitReport hostReportPath
            do! TestContext.Out.WriteAsync($"JUnit report saved to {hostReportPath}")
        with
        | :? FileNotFoundException ->
            do! TestContext.Out.WriteAsync($"Warning: No JUnit report found at {containerReportPath}")
        | ex -> do! TestContext.Out.WriteAsync($"Warning: Failed to copy JUnit report: {ex.Message}")

        do! container.StopAsync(ct)

        return int(exitCode)
    }
