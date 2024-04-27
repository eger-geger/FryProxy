namespace FryProxy.IO.BufferedParser

open System.IO
open System.Threading.Tasks
open FryProxy.IO

type 'a Parser = ReadBuffer * Stream * int -> (int * 'a) option Task
