namespace FryProxy

open System
open System.Net.Sockets
open System.Threading.Tasks
open FryProxy.IO
open FryProxy.Http

type IContext =
    inherit IDisposable

    abstract member RequestHandler: RequestHandler with get, set

    abstract member ResponseHandler: ResponseHandler with get, set

    abstract member ConnectAsync: string * int -> Task<Socket>

    abstract member AllocateBuffer: Socket -> ReadBuffer

and RequestHandler =
    delegate of ctx: IContext * request: RequestMessage * next: RequestHandler -> ResponseMessage ValueTask

and ResponseHandler =
    delegate of ctx: IContext * response: ResponseMessage * next: ResponseHandler -> ResponseMessage ValueTask
