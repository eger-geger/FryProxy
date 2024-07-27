namespace FryProxy

open System.Buffers
open System.Net.Sockets
open System.Threading.Tasks
open FryProxy.IO
open FryProxy.Http
open FryProxy.Extension

/// Keeps track of resources created during request processing.
type Session(stack: ResourceStack, settings: Settings) =

    /// Allocate read buffer wrapping given stream.
    member _.AllocateBuffer stream =
        let mem = MemoryPool.Shared.Rent(settings.BufferSize) |> stack.Push in ReadBuffer(mem.Memory, stream)

    /// Open connection to remote server.
    member _.ConnectAsync(target: Target) =
        task {
            let socket =
                new Socket(
                    SocketType.Stream,
                    ProtocolType.Tcp,
                    BufferSize = settings.BufferSize,
                    Timeouts = settings.UpstreamTimeouts
                )
                |> stack.Push

            do! socket.ConnectAsync(target.Host, ValueOption.defaultValue settings.DefaultRequestPort target.Port)

            return new AsyncTimeoutDecorator(new NetworkStream(socket, true)) |> stack.Push
        }

    /// Creates buffered remote connection.
    member this.ConnectBufferAsync target =
        ValueTask.FromTask
        <| task {
            let! stream = this.ConnectAsync(target)
            return this.AllocateBuffer(stream)
        }
