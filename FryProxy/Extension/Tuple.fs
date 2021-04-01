module System.Tuple

let create2 a b = Tuple.Create(a, b)

let append1 b a = create2 a b

let append2 c (a, b) = a, b, c

let map2of3 fn (a, b, c) = a, fn(b), c