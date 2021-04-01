namespace FryProxy.IO

open System
open System.IO

type ConsReadonlyStream(head: Stream, tail: Stream) =
    inherit Stream()
    let mutable streamRead = false, false

    do
        if not head.CanRead then raise (ArgumentException("Not readable", nameof head))
        if not tail.CanRead then raise (ArgumentException("Not readable", nameof tail))

    override this.Flush() = raise (NotImplementedException())

    override this.Read(buffer, offset, count) =
        match streamRead with
        | (false, false) ->
            match head.Read(buffer, offset, count) with
            | n when n = count -> count
            | n ->
                streamRead <- (true, false)
                n + this.Read(buffer, offset + n, count - n)
        | (true, false) ->
            match tail.Read(buffer, offset, count) with
            | n when n = count -> n
            | n ->
                streamRead <- (true, true)
                n
        | _ -> 0

    override this.Seek(_, _) = raise (NotImplementedException())
    override this.SetLength _ = raise (NotImplementedException())
    override this.Write(_, _, _) = raise (NotImplementedException())
    override this.CanRead = true
    override this.CanSeek = false
    override this.CanWrite = false
    override this.Length = raise (NotImplementedException())

    override this.Position
        with get () = raise (NotImplementedException())
        and set _ = raise (NotImplementedException())
