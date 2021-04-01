module Seq

let isNotEmpty source = source |> (Seq.isEmpty >> not)

let cons head tail = seq {
    yield head
    yield! tail
}

let decompose (source: 'a seq) =
    let iter = source.GetEnumerator()

    let tail =
        seq {
            use src = iter

            while src.MoveNext() do
                yield src.Current
        }

    if iter.MoveNext() then
        Some iter.Current, tail
    else
        iter.Dispose()
        None, Seq.empty
