namespace FryProxy

open System.Buffers
open System.IO
open System.Net.Sockets
open System.Threading.Tasks
open FryProxy.IO
open FryProxy.Http

/// Keeps track of resources created during request processing.
type Context(stack: ResourceStack, handler: RequestHandlerChain, settings: Settings) =

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

            return new NetworkStream(socket, true) |> stack.Push
        }

    /// Complete request handler chain with a reverse proxy handler using provided server connection function.
    member this.CompleteChain(connect: Target -> #Stream Task) =
        let chain =
            if isNull handler then
                RequestHandlerChain.Noop
            else
                handler

        let bufferConnect target =
            task {
                let! stream = connect(target)
                return this.AllocateBuffer(stream)
            }

        chain.Seal(Proxy.reverse bufferConnect)
