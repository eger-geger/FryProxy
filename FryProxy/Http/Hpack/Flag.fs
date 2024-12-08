module FryProxy.Http.Hpack.Flag

let inline check flag value = flag &&& value = flag