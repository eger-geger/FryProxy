namespace FryProxy.Http

open System

/// Simplified URI
[<Struct>]
type Resource = { Host: string; Port: int ValueOption }

/// First HTTP message line.
type StartLine =

    /// HTTP version.
    abstract member Version: Version

    /// Convert to line transmittable over network.
    abstract member Encode: unit -> string

module StartLine =
    let encode (line: StartLine) = line.Encode()
