module FryProxy.Tests.Proxy.H2SpecReport

open System
open System.IO
open System.Xml.Linq

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
