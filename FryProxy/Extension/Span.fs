module FryProxy.Extension.Span

open System

let inline concat2 (a: 'a Span) (b: 'a Span) =
    let dest = Span(Array.zeroCreate(a.Length + b.Length))
    a.CopyTo(dest)
    b.CopyTo(dest.Slice(a.Length))
    dest

let inline concat3 (a: 'a Span) (b: 'a Span) (c: 'a Span) =
    let dest = Span(Array.zeroCreate(a.Length + b.Length + c.Length))
    a.CopyTo(dest)
    b.CopyTo(dest.Slice(a.Length))
    c.CopyTo(dest.Slice(a.Length + b.Length))
    dest

let inline concat4 (a: 'a Span) (b: 'a Span) (c: 'a Span) (d: 'a Span) =
    let dest = Span(Array.zeroCreate(a.Length + b.Length + c.Length + d.Length))
    a.CopyTo(dest)
    b.CopyTo(dest.Slice(a.Length))
    c.CopyTo(dest.Slice(a.Length + b.Length))
    d.CopyTo(dest.Slice(a.Length + b.Length + c.Length))
    dest

let inline concat5 (a: 'a Span) (b: 'a Span) (c: 'a Span) (d: 'a Span) (e: 'a Span) =
    let dest = Span(Array.zeroCreate(a.Length + b.Length + c.Length + d.Length + e.Length))
    a.CopyTo(dest)
    b.CopyTo(dest.Slice(a.Length))
    c.CopyTo(dest.Slice(a.Length + b.Length))
    d.CopyTo(dest.Slice(a.Length + b.Length + c.Length))
    e.CopyTo(dest.Slice(a.Length + b.Length + c.Length + d.Length))
    dest