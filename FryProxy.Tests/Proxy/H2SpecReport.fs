module FryProxy.Tests.Proxy.H2SpecReport

open System
open System.Xml.Linq

let updateElementAttribute (name: XName) (value: string) (el: XElement) =
    let attr = el.Attribute(name)

    if attr = null then
        el.Add(XAttribute(name, value))
    elif String.IsNullOrWhiteSpace(attr.Value) then
        attr.Value <- value

let private fixTestsuiteName (suiteName: XName) (ts: XElement) =
    let nameAttr = ts.Attribute(suiteName)

    if nameAttr = null || String.IsNullOrWhiteSpace(nameAttr.Value) then
        let fallbackName =
            match ts.Attribute(XName.Get("id")) with
            | null -> "Test Suite"
            | attr when String.IsNullOrWhiteSpace(attr.Value) -> "Test Suite"
            | attr -> attr.Value

        do updateElementAttribute suiteName fallbackName ts

let private fixTestcaseName (testName: XName) (className: XName) (tc: XElement) =
    let classAttr = tc.Attribute(className)

    if classAttr <> null && not (String.IsNullOrWhiteSpace(classAttr.Value)) then
        do updateElementAttribute testName classAttr.Value tc

/// Fixes h2spec JUnit XML reports so that dorny/test-reporter@v3 can parse them.
/// h2spec generates reports with missing or empty `name` attributes on <testsuite>
/// and <testcase> elements, which cause the parser to crash with
/// `TypeError: Cannot read properties of undefined (reading 'trim')`.
let fixH2SpecJUnitReport (path: string) =
    let doc = XDocument.Load(path)
    let nameXName = XName.Get("name")
    let classXName = XName.Get("classname")

    doc.Descendants(XName.Get("testsuite")) |> Seq.iter (fixTestsuiteName nameXName)

    doc.Descendants(XName.Get("testcase"))
    |> Seq.iter (fixTestcaseName nameXName classXName)

    doc.Save(path)
