namespace FryProxy.Pipeline

open System.Threading.Tasks
open FryProxy.Http


// Async HTTP response message along with a context returned by handler or chain.
type 'Context ContextualResponse = (ResponseMessage * 'Context) ValueTask

/// Function handling proxied HTTP request. Receives request and returns HTTP response message sent to a client.
type 'O RequestHandler = delegate of request: RequestMessage -> 'O ContextualResponse

/// Request handler chain of responsibility.
type 'O RequestHandlerChain = delegate of request: RequestMessage * next: 'O RequestHandler -> 'O ContextualResponse

[<AutoOpen>]
module RequestHandlerExtensions =

    type 'O RequestHandlerChain with

        /// Invokes the next handler.
        static member inline Noop() = RequestHandlerChain(fun r h -> h.Invoke(r))

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
