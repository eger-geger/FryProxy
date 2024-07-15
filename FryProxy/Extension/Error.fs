﻿[<AutoOpen>]
module FryProxy.Extension.Error

open System
open System.Runtime.ExceptionServices

type Exception with
    
    /// Raise the error with original stacktrace.
    member err.Rethrow() =
        ExceptionDispatchInfo.Throw(err)
        Unchecked.defaultof<_>