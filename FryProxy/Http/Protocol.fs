module FryProxy.Http.Protocol

open System

let Http10 = Version(1, 0)
let Http11 = Version(1, 1)

/// Deconstruct major and minor protocol version.
let (|ProtocolVersion|) (ver: Version) = ProtocolVersion(ver.Major, ver.Minor)
