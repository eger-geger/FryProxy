module FryProxy.Tests.Proxy.H2SpecReport

open System
open System.Xml.Linq

let updateElementAttribute (name: XName) (value: string) (el: XElement) =
    let attr = el.Attribute(name)

    if attr = null then
        el.Add(XAttribute(name, value))
    elif String.IsNullOrWhiteSpace(attr.Value) then
        attr.Value <- value

let private fixTestsuiteName (ts: XElement) (nameXName: XName) =
    let nameAttr = ts.Attribute(nameXName)

    if nameAttr = null || String.IsNullOrWhiteSpace(nameAttr.Value) then
        let idAttr = ts.Attribute(XName.Get("id"))

        let fallbackName =
            match idAttr with
            | null -> "Test Suite"
            | _ when String.IsNullOrWhiteSpace(idAttr.Value) -> "Test Suite"
            | _ -> idAttr.Value

        do updateElementAttribute nameXName fallbackName ts

let private fixTestcaseName (tc: XElement) (nameXName: XName) (classXName: XName) =
    let classAttr = tc.Attribute(classXName)

    if classAttr <> null && not (String.IsNullOrWhiteSpace(classAttr.Value)) then
        do updateElementAttribute nameXName classAttr.Value tc

/// Fixes h2spec JUnit XML reports so that dorny/test-reporter@v3 can parse them.
/// h2spec generates reports with missing or empty `name` attributes on <testsuite>
/// and <testcase> elements, which cause the parser to crash with
/// `TypeError: Cannot read properties of undefined (reading 'trim')`.
let fixH2SpecJUnitReport (path: string) =
    let doc = XDocument.Load(path)
    let nameXName = XName.Get("name")
    let classXName = XName.Get("classname")

    doc.Descendants(XName.Get("testsuite"))
    |> Seq.iter (fun ts -> fixTestsuiteName ts nameXName)

    doc.Descendants(XName.Get("testcase"))
    |> Seq.iter (fun tc -> fixTestcaseName tc nameXName classXName)

    doc.Save(path)
