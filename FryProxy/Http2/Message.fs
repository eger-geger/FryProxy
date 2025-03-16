module FryProxy.Http2.Message

open System.Collections.Generic
open FryProxy.Http2.Frames

let decode (frames: Frame IAsyncEnumerable) =
    task {
        use enum = frames.GetAsyncEnumerator()
        
        while! enum.MoveNextAsync() do
            invalidOp "TODO"
    }

