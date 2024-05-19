namespace FryProxy.Http

open System

/// Simplified URI
type Resource = { Host: string; Port: int; AbsoluteRef: string }

/// First HTTP message line.
type StartLine =

    /// HTTP version.
    abstract member Version: Version

    /// Convert to line transmittable over network.
    abstract member Encode: unit -> string

module StartLine =
    let encode (line: StartLine) = line.Encode()
