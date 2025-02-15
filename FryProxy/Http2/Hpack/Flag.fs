module FryProxy.Http2.Hpack.Flag

let inline check flag value = flag &&& value = flag