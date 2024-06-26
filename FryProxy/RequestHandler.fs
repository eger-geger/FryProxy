namespace FryProxy

open System.Threading.Tasks
open FryProxy.Http

/// Function handling proxied HTTP request. Receives request and returns HTTP response message sent to a client.
type RequestHandler = delegate of request: RequestMessage -> ResponseMessage ValueTask

/// Request handler chain of responsibility.
type RequestHandlerChain = delegate of request: RequestMessage * next: RequestHandler -> ResponseMessage ValueTask

[<AutoOpen>]
module RequestHandlerExtensions =
    type RequestHandlerChain with

        /// Invokes the next handler.
        static member inline Noop = RequestHandlerChain(fun r h -> h.Invoke(r))

        /// Seal the chain from modifications by giving it default request handler.
        member inline chain.Seal(root: RequestHandler) : RequestHandler =
            RequestHandler(fun request -> chain.Invoke(request, root))

        /// Combine 2 chains by executing outer first passing it inner as an argument.
        static member inline Join(outer: RequestHandlerChain, inner: RequestHandlerChain) : RequestHandlerChain =
            match outer, inner with
            | null, h -> h
            | h, null -> h
            | a, b -> RequestHandlerChain(fun r next -> a.Invoke(r, RequestHandler(fun r' -> b.Invoke(r', next))))
