namespace FryProxy.IO

open System.Threading.Tasks

type IConsumable =

    abstract member Consumed: ValueTask
