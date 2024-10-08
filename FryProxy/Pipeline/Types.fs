namespace FryProxy.Pipeline

open System.Threading.Tasks
open FryProxy.Http
open FryProxy.Extension


// Async HTTP response message along with a context returned by handler or chain.
type 'Context ContextualResponse = (ResponseMessage * 'Context) ValueTask

/// Function handling proxied HTTP request. Receives request and returns HTTP response message sent to a client.
type 'O RequestHandler = delegate of request: RequestMessage -> 'O ContextualResponse

/// Request handler chain of responsibility.
type 'O RequestHandlerChain = delegate of request: RequestMessage * next: 'O RequestHandler -> 'O ContextualResponse

/// Propagates whether client wants to reuse a connection after receiving a response message.
type 'T IClientConnectionAware when 'T :> IClientConnectionAware<'T> =

    /// Record whether client wants to reuse a connection after receiving a response message.
    abstract WithKeepClientConnection: bool -> 'T

    /// Report whether client wants to reuse a connection after receiving a response message.
    abstract member KeepClientConnection: bool

/// Propagates weather upstream allows reusing connection after sending a response message.
type 'T IUpstreamConnectionAware when 'T :> IUpstreamConnectionAware<'T> =

    /// Record weather upstream allows reusing connection after sending a response message.
    abstract WithKeepUpstreamConnection: bool -> 'T

    /// Report weather upstream allows reusing connection after sending a response message.
    abstract member KeepUpstreamConnection: bool

/// Propagates established tunnel along response message.
type ('Tunnel, 'T) ITunnelAware when 'T :> ITunnelAware<'Tunnel, 'T> and 'T: (new: unit -> 'T) =
    /// Receives an established tunnel.
    abstract WithTunnel: 'Tunnel -> 'T

    /// Exposes established tunnel, if any.
    abstract member Tunnel: 'Tunnel voption

module RequestHandler =

    /// Adds empty context to HTTP response message.
    let toContextual resp : 'T ContextualResponse = ValueTask.FromResult(resp, new 'T())

    /// Convert a context-less request handler to a contextual one with a newly initialized context.
    let withContext (next: 'a -> ResponseMessage ValueTask) arg : 'T ContextualResponse =
        ValueTask.FromTask
        <| task {
            let! resp = next arg
            return resp, new 'T()
        }

[<AutoOpen>]
module RequestHandlerExtensions =

    type 'O RequestHandlerChain with

        /// Invokes the next handler.
        static member inline Noop() =
            RequestHandlerChain(fun r h -> h.Invoke(r))

        /// Combine 2 chains by executing outer first passing it inner as an argument.
        static member inline Join(outer: _ RequestHandlerChain, inner: _ RequestHandlerChain) : _ RequestHandlerChain =
            match outer, inner with
            | null, h -> h
            | h, null -> h
            | a, b -> RequestHandlerChain(fun r next -> a.Invoke(r, RequestHandler(fun r' -> b.Invoke(r', next))))

        /// Wrap current handler into another, making current inner.
        member inline this.WrapInto outer = RequestHandlerChain.Join(outer, this)

        /// Wrap current handler over another, making current outer.
        member inline this.WrapOver inner = RequestHandlerChain.Join(this, inner)

        /// Seal the chain from modifications by giving it default request handler.
        member inline chain.Seal(root: _ RequestHandler) : _ RequestHandler =
            RequestHandler(fun request -> chain.Invoke(request, root))

    /// Composes chains by passing second (right) as an argument to the first (left).
    let inline (+>) outer inner = RequestHandlerChain.Join(outer, inner)
