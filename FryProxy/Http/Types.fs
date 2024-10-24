namespace FryProxy.Http

open System

/// Request destination host and optional port
[<Struct>]
type Target =
    { Host: string
      Port: int ValueOption }

    override this.ToString() =
        let suffix =
            this.Port |> ValueOption.map(sprintf ":%d") |> ValueOption.defaultValue ""

        this.Host + suffix

[<RequireQualifiedAccess>]
module Target =

    // Initialize request target from host and port.
    let create host port = { Host = host; Port = port }

/// First HTTP message line.
type StartLine =

    /// HTTP version.
    abstract member Version: Version

    /// Convert to line transmittable over network.
    abstract member Encode: unit -> string

module StartLine =
    let encode (line: StartLine) = line.Encode()
