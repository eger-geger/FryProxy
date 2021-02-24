module Seq

let isNotEmpty source = source |> (Seq.isEmpty >> not)

let decompose (source: 'a seq) =
    let enum = source.GetEnumerator()

    let tail =
        seq {
            use src = enum

            while src.MoveNext() do
                yield src.Current
        }

    if enum.MoveNext() then
        Some enum.Current, tail
    else
        enum.Dispose()
        None, Seq.empty
