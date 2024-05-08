namespace FryProxy.IO.BufferedParser

open System.IO
open System.Threading.Tasks
open FryProxy.IO

type ('a, 's) Parser when 's :> Stream = ReadBuffer<'s> * int -> (int * 'a) option Task
